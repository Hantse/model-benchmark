using Benchmark.Domain;
namespace Benchmark.Application;

public sealed class WorkItemService(IWorkItemRepository repository)
{
    public async Task<IReadOnlyList<WorkItem>> ListAsync(string owner, WorkStatus? status = null, CancellationToken ct = default)
    {
        var items = await repository.ListAsync(owner, ct).ConfigureAwait(false);
        return status is null ? items : items.Where(i => i.Status == status.Value).ToList();
    }

    public async Task<WorkItem> AddAsync(string owner, string title, WorkStatus status, CancellationToken ct = default)
    {
        var trimmed = title.Trim();
        if (trimmed.Length < 1 || trimmed.Length > 120)
            throw new ArgumentException("Title must be 1-120 characters.", nameof(title));
        var item = new WorkItem(Guid.NewGuid(), owner, trimmed, status, 1);
        await repository.AddAsync(item, ct).ConfigureAwait(false);
        return item;
    }

    public async Task<WorkItem> UpdateAsync(string owner, Guid id, string title, WorkStatus status, int expectedVersion, CancellationToken ct = default)
    {
        if (expectedVersion < 1)
            throw new ArgumentException("Expected version must be positive.", nameof(expectedVersion));
        var trimmed = title.Trim();
        if (trimmed.Length < 1 || trimmed.Length > 120)
            throw new ArgumentException("Title must be 1-120 characters.", nameof(title));
        var current = await repository.FindAsync(id, ct).ConfigureAwait(false);
        if (current is null || current.Owner != owner)
            throw new ItemNotFoundException();
        var replacement = new WorkItem(id, owner, trimmed, status, current.Version + 1);
        var ok = await repository.ReplaceAsync(replacement, expectedVersion, ct).ConfigureAwait(false);
        if (!ok)
            throw new VersionConflictException();
        return replacement;
    }

    public async Task DeleteAsync(string owner, Guid id, int expectedVersion, CancellationToken ct = default)
    {
        if (expectedVersion < 1)
            throw new ArgumentException("Expected version must be positive.", nameof(expectedVersion));
        var current = await repository.FindAsync(id, ct).ConfigureAwait(false);
        if (current is null || current.Owner != owner)
            throw new ItemNotFoundException();
        var ok = await repository.DeleteAsync(id, owner, expectedVersion, ct).ConfigureAwait(false);
        if (!ok)
            throw new VersionConflictException();
    }
}
