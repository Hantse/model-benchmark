using Benchmark.Domain;
namespace Benchmark.Application;
public sealed class WorkItemService(IWorkItemRepository repository)
{
    public Task<IReadOnlyList<WorkItem>> ListAsync(string owner, WorkStatus? status = null, CancellationToken ct = default)
    {
        var items = repository.ListAsync(owner, ct).GetAwaiter().GetResult();
        var filtered = status is null ? items : items.Where(i => i.Status == status).ToArray();
        return Task.FromResult<IReadOnlyList<WorkItem>>(filtered);
    }

    public Task<WorkItem> AddAsync(string owner, string title, WorkStatus status, CancellationToken ct = default)
    {
        var trimmed = title?.Trim() ?? string.Empty;
        if (trimmed.Length < 1 || trimmed.Length > 120) throw new ArgumentException("Title must be 1-120 characters.");
        var item = new WorkItem(Guid.NewGuid(), owner, trimmed, status, 1);
        repository.AddAsync(item, ct).GetAwaiter().GetResult();
        return Task.FromResult(item);
    }

    public Task<WorkItem> UpdateAsync(string owner, Guid id, string title, WorkStatus status, int expectedVersion, CancellationToken ct = default)
    {
        var trimmed = title?.Trim() ?? string.Empty;
        if (trimmed.Length < 1 || trimmed.Length > 120) throw new ArgumentException("Title must be 1-120 characters.");
        if (expectedVersion < 1) throw new ArgumentException("Expected version must be positive.");
        var existing = repository.FindAsync(id, ct).GetAwaiter().GetResult();
        if (existing is null || existing.Owner != owner) throw new ItemNotFoundException();
        var replacement = existing with { Title = trimmed, Status = status, Version = expectedVersion + 1 };
        var ok = repository.ReplaceAsync(replacement, expectedVersion, ct).GetAwaiter().GetResult();
        if (!ok) throw new VersionConflictException();
        return Task.FromResult(replacement);
    }

    public Task DeleteAsync(string owner, Guid id, int expectedVersion, CancellationToken ct = default)
    {
        if (expectedVersion < 1) throw new ArgumentException("Expected version must be positive.");
        var existing = repository.FindAsync(id, ct).GetAwaiter().GetResult();
        if (existing is null || existing.Owner != owner) throw new ItemNotFoundException();
        var ok = repository.DeleteAsync(id, owner, expectedVersion, ct).GetAwaiter().GetResult();
        if (!ok) throw new VersionConflictException();
        return Task.CompletedTask;
    }
}
