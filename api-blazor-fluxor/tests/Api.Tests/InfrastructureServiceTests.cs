using Benchmark.Domain;
using Benchmark.Infrastructure;
using Xunit;

namespace Benchmark.Tests;

public sealed class InfrastructureServiceTests
{
    private static WorkItem Item(string owner = "alice") => new(Guid.NewGuid(), owner, "Original", WorkStatus.Todo, 1);

    [Fact]
    public async Task Add_uses_the_injected_repository_and_normalizes_the_title()
    {
        var repository = new RecordingRepository();
        var item = await new WorkItemService(repository).AddAsync("alice", "  New item  ", WorkStatus.Todo);
        Assert.Equal("New item", item.Title); Assert.Equal("alice", item.Owner); Assert.Equal(1, item.Version);
        Assert.NotEqual(Guid.Empty, item.Id); Assert.Equal(item, repository.Items.Single()); Assert.Equal(1, repository.Writes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task Empty_or_missing_titles_are_rejected_before_writing(string? title)
    {
        var repository = new RecordingRepository();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => new WorkItemService(repository).AddAsync("alice", title!, WorkStatus.Todo));
        Assert.Equal(0, repository.Writes);
    }

    [Fact]
    public async Task Oversized_title_is_rejected_before_writing()
    {
        var repository = new RecordingRepository();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => new WorkItemService(repository).AddAsync("alice", new string('x', 121), WorkStatus.Todo));
        Assert.Equal(0, repository.Writes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(120)]
    public async Task Trimmed_title_boundaries_are_accepted(int length)
    {
        var repository = new RecordingRepository(); var title = new string('x', length);
        Assert.Equal(title, (await new WorkItemService(repository).AddAsync("alice", "  " + title + "  ", WorkStatus.Done)).Title);
    }

    [Fact]
    public async Task List_applies_the_requested_status_to_repository_data()
    {
        var repository = new RecordingRepository(); repository.Items.Add(Item()); repository.Items.Add(Item() with { Status = WorkStatus.Done });
        var items = await new WorkItemService(repository).ListAsync("alice", WorkStatus.Done);
        Assert.Single(items); Assert.Equal(WorkStatus.Done, items[0].Status); Assert.Equal(1, repository.ListCalls);
    }

    [Fact]
    public async Task List_without_status_keeps_all_of_the_owners_statuses()
    {
        var repository = new RecordingRepository(); repository.Items.Add(Item()); repository.Items.Add(Item() with { Status = WorkStatus.Done }); repository.Items.Add(Item("bob"));
        var items = await new WorkItemService(repository).ListAsync("alice");
        Assert.Equal(2, items.Count); Assert.All(items, item => Assert.Equal("alice", item.Owner));
    }

    [Theory]
    [InlineData("add")]
    [InlineData("update")]
    [InlineData("list")]
    public async Task Undefined_enum_status_is_rejected_without_mutation(string operation)
    {
        var repository = new RecordingRepository(); var item = Item(); repository.Items.Add(item); var service = new WorkItemService(repository); var invalid = (WorkStatus)99;
        Task Act() => operation switch { "add" => service.AddAsync("alice", "Valid", invalid), "update" => service.UpdateAsync("alice", item.Id, "Valid", invalid, 1), _ => service.ListAsync("alice", invalid) };
        await Assert.ThrowsAnyAsync<ArgumentException>(Act); Assert.Equal(0, repository.Writes); Assert.Equal(item, repository.Items.Single());
    }

    [Fact]
    public async Task Ownership_is_checked_before_version_conflict_for_update_and_delete()
    {
        var repository = new RecordingRepository(); var item = Item(); repository.Items.Add(item); var service = new WorkItemService(repository);
        await Assert.ThrowsAsync<ItemNotFoundException>(() => service.UpdateAsync("bob", item.Id, "Foreign", WorkStatus.Done, 999));
        await Assert.ThrowsAsync<ItemNotFoundException>(() => service.DeleteAsync("bob", item.Id, 999));
        Assert.Equal(0, repository.Writes); Assert.Equal(item, repository.Items.Single());
    }

    [Fact]
    public async Task Missing_ids_are_not_found_for_update_and_delete()
    {
        var repository = new RecordingRepository(); var service = new WorkItemService(repository); var id = Guid.NewGuid();
        await Assert.ThrowsAsync<ItemNotFoundException>(() => service.UpdateAsync("alice", id, "Missing", WorkStatus.Todo, 1));
        await Assert.ThrowsAsync<ItemNotFoundException>(() => service.DeleteAsync("alice", id, 1)); Assert.Equal(0, repository.Writes);
    }

    [Fact]
    public async Task Update_normalizes_and_delegates_atomic_compare_and_swap()
    {
        var repository = new RecordingRepository(); var item = Item(); repository.Items.Add(item);
        var next = await new WorkItemService(repository).UpdateAsync("alice", item.Id, "  New  ", WorkStatus.InProgress, 1);
        Assert.Equal(item with { Title = "New", Status = WorkStatus.InProgress, Version = 2 }, next);
        Assert.Equal(1, repository.LastExpectedVersion); Assert.Equal(1, repository.Writes);
    }

    [Fact]
    public async Task Update_repository_race_reports_conflict_without_retry()
    {
        var repository = new RecordingRepository { RejectReplace = true }; var item = Item(); repository.Items.Add(item);
        await Assert.ThrowsAsync<VersionConflictException>(() => new WorkItemService(repository).UpdateAsync("alice", item.Id, "New", WorkStatus.Done, 1));
        Assert.Equal(1, repository.Writes); Assert.Equal(item, repository.Items.Single());
    }

    [Fact]
    public async Task Stale_update_and_delete_preserve_the_current_entity_and_report_conflict()
    {
        var repository = new RecordingRepository(); var item = Item() with { Title = "Latest", Version = 2 }; repository.Items.Add(item);
        var service = new WorkItemService(repository);
        await Assert.ThrowsAsync<VersionConflictException>(() => service.UpdateAsync("alice", item.Id, "Stale", WorkStatus.Done, 1));
        await Assert.ThrowsAsync<VersionConflictException>(() => service.DeleteAsync("alice", item.Id, 1));
        Assert.Equal(item, repository.Items.Single()); Assert.InRange(repository.Writes, 0, 2);
    }

    [Fact]
    public async Task Delete_passes_the_expected_version_to_the_repository()
    {
        var repository = new RecordingRepository(); var item = Item(); repository.Items.Add(item);
        await new WorkItemService(repository).DeleteAsync("alice", item.Id, 1);
        Assert.Empty(repository.Items); Assert.Equal(1, repository.LastExpectedVersion); Assert.Equal(1, repository.Writes);
    }

    [Fact]
    public async Task Delete_repository_race_reports_conflict_without_retry()
    {
        var repository = new RecordingRepository { RejectDelete = true }; var item = Item(); repository.Items.Add(item);
        await Assert.ThrowsAsync<VersionConflictException>(() => new WorkItemService(repository).DeleteAsync("alice", item.Id, 1));
        Assert.Equal(1, repository.Writes); Assert.Equal(item, repository.Items.Single());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Nonpositive_update_version_is_invalid_before_writing(int version)
    {
        var repository = new RecordingRepository(); var item = Item(); repository.Items.Add(item);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => new WorkItemService(repository).UpdateAsync("alice", item.Id, "New", WorkStatus.Done, version));
        Assert.Equal(0, repository.Writes); Assert.Equal(item, repository.Items.Single());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Nonpositive_delete_version_is_invalid_before_writing(int version)
    {
        var repository = new RecordingRepository(); var item = Item(); repository.Items.Add(item);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => new WorkItemService(repository).DeleteAsync("alice", item.Id, version));
        Assert.Equal(0, repository.Writes); Assert.Equal(item, repository.Items.Single());
    }

    [Fact]
    public async Task List_forwards_the_cancellation_token_to_the_repository()
    {
        var repository = new RecordingRepository(); using var cancellation = new CancellationTokenSource();
        await new WorkItemService(repository).ListAsync("alice", ct: cancellation.Token);
        Assert.Equal(cancellation.Token, repository.LastToken);
    }

    private sealed class RecordingRepository : IWorkItemRepository
    {
        public List<WorkItem> Items { get; } = [];
        public int Writes, ListCalls, LastExpectedVersion;
        public bool RejectReplace, RejectDelete;
        public CancellationToken LastToken;
        public Task<IReadOnlyList<WorkItem>> ListAsync(string owner, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); LastToken = ct; ListCalls++; return Task.FromResult<IReadOnlyList<WorkItem>>(Items.Where(item => item.Owner == owner).ToArray()); }
        public Task<WorkItem?> FindAsync(Guid id, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); LastToken = ct; return Task.FromResult(Items.SingleOrDefault(item => item.Id == id)); }
        public Task AddAsync(WorkItem item, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); LastToken = ct; Writes++; Items.Add(item); return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(WorkItem replacement, int expectedVersion, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested(); LastToken = ct; Writes++; LastExpectedVersion = expectedVersion;
            if (RejectReplace) return Task.FromResult(false);
            var index = Items.FindIndex(item => item.Id == replacement.Id && item.Owner == replacement.Owner && item.Version == expectedVersion);
            if (index < 0) return Task.FromResult(false); Items[index] = replacement; return Task.FromResult(true);
        }
        public Task<bool> DeleteAsync(Guid id, string owner, int expectedVersion, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested(); LastToken = ct; Writes++; LastExpectedVersion = expectedVersion;
            return Task.FromResult(!RejectDelete && Items.RemoveAll(item => item.Id == id && item.Owner == owner && item.Version == expectedVersion) == 1);
        }
    }
}
