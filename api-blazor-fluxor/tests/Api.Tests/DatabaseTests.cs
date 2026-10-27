using Benchmark.Domain;
using Benchmark.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Benchmark.Tests;

// Each case uses its own physical database. Pooling is disabled so cleanup also
// works on Windows; competing repositories still open independent connections.
public sealed class DatabaseTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "work-board-db-" + Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(directory, "work-items.db");
    private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false, DefaultTimeout = 15 }.ToString();

    public DatabaseTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, recursive: true);
    private SqliteWorkItemRepository Repository() => new(ConnectionString);
    private async Task<SqliteWorkItemRepository> Ready()
    {
        var repository = Repository();
        await repository.InitializeAsync();
        return repository;
    }
    private static WorkItem Item(string owner = "alice", string title = "Original", WorkStatus status = WorkStatus.Todo) => new(Guid.NewGuid(), owner, title, status, 1);

    [Fact]
    public async Task Initialization_creates_a_real_SQLite_database()
    {
        await Ready();
        Assert.True(File.Exists(DatabasePath));
        using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
        Assert.True(Convert.ToInt64(await command.ExecuteScalarAsync()) > 0);
    }

    [Fact]
    public async Task Initialization_is_idempotent_and_preserves_existing_rows()
    {
        var repository = await Ready(); var item = Item(); await repository.AddAsync(item);
        await repository.InitializeAsync(); await Repository().InitializeAsync();
        Assert.Equal(item, await Repository().FindAsync(item.Id));
    }

    [Fact]
    public async Task Fresh_database_has_an_empty_owner_list() => Assert.Empty(await (await Ready()).ListAsync("alice"));

    [Fact]
    public async Task A_new_repository_reads_previously_inserted_data()
    {
        var repository = await Ready(); var item = Item(); await repository.AddAsync(item);
        var reopened = Repository(); await reopened.InitializeAsync();
        Assert.Equal(item, await reopened.FindAsync(item.Id));
        Assert.Equal(new[] { item }, await reopened.ListAsync("alice"));
    }

    [Theory]
    [InlineData(WorkStatus.Todo)]
    [InlineData(WorkStatus.InProgress)]
    [InlineData(WorkStatus.Done)]
    public async Task Stored_fields_round_trip_with_each_status(WorkStatus status)
    {
        var repository = await Ready(); var item = Item(status: status) with { Version = 7 };
        await repository.AddAsync(item);
        Assert.Equal(item, await Repository().FindAsync(item.Id));
    }

    [Fact]
    public async Task Unicode_quotes_and_SQL_looking_titles_are_data()
    {
        var repository = await Ready(); var item = Item(title: "Café 🛠 O'Brien'); DROP TABLE work_items; --");
        await repository.AddAsync(item); await repository.AddAsync(Item(title: "Still writable"));
        Assert.Equal(item, await Repository().FindAsync(item.Id));
        Assert.Equal(2, (await Repository().ListAsync("alice")).Count);
    }

    [Fact]
    public async Task Connection_strings_select_independent_database_files()
    {
        var repository = await Ready(); await repository.AddAsync(Item());
        var other = new SqliteWorkItemRepository(new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "other.db"), Pooling = false }.ToString());
        await other.InitializeAsync();
        Assert.Empty(await other.ListAsync("alice")); Assert.Single(await repository.ListAsync("alice"));
    }

    [Fact]
    public async Task Owner_filter_never_returns_another_users_rows()
    {
        var repository = await Ready(); var alice = Item(); var bob = Item("bob");
        await repository.AddAsync(alice); await repository.AddAsync(bob);
        Assert.Equal(new[] { alice }, await Repository().ListAsync("alice"));
        Assert.Equal(new[] { bob }, await Repository().ListAsync("bob"));
    }

    [Fact]
    public async Task Owner_comparison_is_exact_and_case_sensitive()
    {
        var repository = await Ready(); await repository.AddAsync(Item("Alice"));
        Assert.Empty(await repository.ListAsync("alice")); Assert.Single(await repository.ListAsync("Alice"));
    }

    [Fact]
    public async Task SQL_looking_owner_cannot_expand_a_list_query()
    {
        var repository = await Ready(); await repository.AddAsync(Item()); var owner = "' OR 1=1 --"; var own = Item(owner);
        await repository.AddAsync(own);
        Assert.Equal(new[] { own }, await repository.ListAsync(owner));
    }

    [Fact]
    public async Task A_list_is_a_snapshot_after_later_database_changes()
    {
        var repository = await Ready(); var item = Item(); await repository.AddAsync(item);
        var before = await repository.ListAsync("alice"); await Repository().DeleteAsync(item.Id, "alice", 1);
        Assert.Equal(new[] { item }, before); Assert.Empty(await repository.ListAsync("alice"));
    }

    [Fact]
    public async Task Missing_id_returns_null() => Assert.Null(await (await Ready()).FindAsync(Guid.NewGuid()));

    [Fact]
    public async Task Duplicate_identity_is_rejected_without_overwriting_data()
    {
        var repository = await Ready(); var item = Item(); await repository.AddAsync(item);
        await Assert.ThrowsAsync<SqliteException>(() => Repository().AddAsync(item with { Title = "Overwrite" }));
        Assert.Equal(item, await Repository().FindAsync(item.Id));
    }

    [Fact]
    public async Task Successful_replace_persists_title_status_and_incremented_version()
    {
        var repository = await Ready(); var item = Item(); await repository.AddAsync(item); var next = item with { Title = "Updated", Status = WorkStatus.Done, Version = 2 };
        Assert.True(await Repository().ReplaceAsync(next, 1)); Assert.Equal(next, await Repository().FindAsync(item.Id));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task Replacement_must_increase_the_expected_version_by_exactly_one(int replacementVersion)
    {
        var repository = await Ready(); var item = Item(); await repository.AddAsync(item);
        Assert.False(await Repository().ReplaceAsync(item with { Title = "Invalid increment", Version = replacementVersion }, 1));
        Assert.Equal(item, await Repository().FindAsync(item.Id));
    }

    [Fact]
    public async Task Replace_of_a_missing_id_returns_false() => Assert.False(await (await Ready()).ReplaceAsync(Item() with { Version = 2 }, 1));

    [Fact]
    public async Task Stale_replace_returns_false_without_mutation()
    {
        var repository = await Ready(); var item = Item() with { Version = 2 }; await repository.AddAsync(item);
        Assert.False(await repository.ReplaceAsync(item with { Title = "Stale" }, 1)); Assert.Equal(item, await Repository().FindAsync(item.Id));
    }

    [Fact]
    public async Task Foreign_replace_returns_false_without_transferring_ownership()
    {
        var repository = await Ready(); var item = Item(); await repository.AddAsync(item);
        Assert.False(await repository.ReplaceAsync(item with { Owner = "bob", Title = "Stolen", Version = 2 }, 1));
        Assert.Equal(item, await Repository().FindAsync(item.Id)); Assert.Empty(await repository.ListAsync("bob"));
    }

    [Fact]
    public async Task Replace_changes_only_the_selected_row()
    {
        var repository = await Ready(); var a = Item(); var b = Item(); await repository.AddAsync(a); await repository.AddAsync(b);
        Assert.True(await repository.ReplaceAsync(a with { Title = "Changed", Version = 2 }, 1)); Assert.Equal(b, await Repository().FindAsync(b.Id));
    }

    [Fact]
    public async Task Successful_delete_remains_deleted_after_reopening()
    {
        var repository = await Ready(); var item = Item(); await repository.AddAsync(item);
        Assert.True(await Repository().DeleteAsync(item.Id, item.Owner, 1)); Assert.Null(await Repository().FindAsync(item.Id));
        Assert.False(await Repository().DeleteAsync(item.Id, item.Owner, 1));
    }

    [Fact]
    public async Task Delete_of_missing_id_returns_false() => Assert.False(await (await Ready()).DeleteAsync(Guid.NewGuid(), "alice", 1));

    [Fact]
    public async Task Stale_delete_returns_false_and_keeps_the_row()
    {
        var repository = await Ready(); var item = Item() with { Version = 2 }; await repository.AddAsync(item);
        Assert.False(await repository.DeleteAsync(item.Id, "alice", 1)); Assert.Equal(item, await Repository().FindAsync(item.Id));
    }

    [Fact]
    public async Task Foreign_delete_returns_false_and_keeps_the_row()
    {
        var repository = await Ready(); var item = Item(); await repository.AddAsync(item);
        Assert.False(await repository.DeleteAsync(item.Id, "bob", 1)); Assert.Equal(item, await Repository().FindAsync(item.Id));
    }

    [Fact]
    public async Task Concurrent_updates_on_independent_connections_have_one_winner()
    {
        var repository = await Ready(); var item = Item(); await repository.AddAsync(item);
        var results = await Compete(16, index => Repository().ReplaceAsync(item with { Title = $"Winner {index}", Version = 2 }, 1));
        Assert.Single(results, success => success); var winner = Array.FindIndex(results, success => success);
        Assert.Equal(item with { Title = $"Winner {winner}", Version = 2 }, await Repository().FindAsync(item.Id));
    }

    [Fact]
    public async Task Concurrent_deletes_on_independent_connections_have_one_winner()
    {
        var repository = await Ready(); var item = Item(); await repository.AddAsync(item);
        var results = await Compete(16, _ => Repository().DeleteAsync(item.Id, item.Owner, 1));
        Assert.Single(results, success => success); Assert.Null(await Repository().FindAsync(item.Id));
    }

    [Fact]
    public async Task Concurrent_update_and_delete_share_the_same_version_guard()
    {
        var repository = await Ready(); var item = Item(); await repository.AddAsync(item);
        var results = await Compete(2, index => index == 0 ? Repository().ReplaceAsync(item with { Title = "Updated", Version = 2 }, 1) : Repository().DeleteAsync(item.Id, item.Owner, 1));
        Assert.Single(results, success => success);
        if (results[0]) Assert.Equal(item with { Title = "Updated", Version = 2 }, await Repository().FindAsync(item.Id));
        else Assert.Null(await Repository().FindAsync(item.Id));
    }

    [Theory]
    [InlineData("initialize")]
    [InlineData("list")]
    [InlineData("find")]
    [InlineData("add")]
    [InlineData("replace")]
    [InlineData("delete")]
    public async Task Precancelled_operations_observe_cancellation_without_mutation(string operation)
    {
        var repository = await Ready(); var item = Item(); await repository.AddAsync(item);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); var ct = cancellation.Token;
        Task Act() => operation switch
        {
            "initialize" => repository.InitializeAsync(ct), "list" => repository.ListAsync("alice", ct),
            "find" => repository.FindAsync(item.Id, ct), "add" => repository.AddAsync(Item(), ct),
            "replace" => repository.ReplaceAsync(item with { Title = "Cancelled", Version = 2 }, 1, ct),
            _ => repository.DeleteAsync(item.Id, "alice", 1, ct)
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(Act);
        Assert.Equal(new[] { item }, await Repository().ListAsync("alice"));
    }

    [Fact]
    public async Task Readonly_database_write_reports_storage_error_without_memory_fallback()
    {
        var repository = await Ready(); var item = Item(); await repository.AddAsync(item);
        var readonlyString = new SqliteConnectionStringBuilder(ConnectionString) { Mode = SqliteOpenMode.ReadOnly }.ToString();
        await Assert.ThrowsAsync<SqliteException>(() => new SqliteWorkItemRepository(readonlyString).AddAsync(Item()));
        Assert.Equal(new[] { item }, await Repository().ListAsync("alice"));
    }

    private static async Task<bool[]> Compete(int callers, Func<int, Task<bool>> operation)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var waiting = 0;
        var attempts = Enumerable.Range(0, callers).Select(index => Task.Run(async () =>
        {
            if (Interlocked.Increment(ref waiting) == callers) ready.SetResult();
            await start.Task; return await operation(index);
        })).ToArray();
        try { await ready.Task.WaitAsync(TimeSpan.FromSeconds(15)); } finally { start.TrySetResult(); }
        return await Task.WhenAll(attempts).WaitAsync(TimeSpan.FromSeconds(45));
    }
}
