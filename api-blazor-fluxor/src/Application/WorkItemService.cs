using Benchmark.Domain;
namespace Benchmark.Application;
public sealed class WorkItemService(IWorkItemRepository repository)
{
    public async Task<IReadOnlyList<WorkItem>> ListAsync(string owner, WorkStatus? status = null, CancellationToken ct = default)
    {
        var items = await repository.ListAsync(owner, ct);
        return status is null ? items : items.Where(i => i.Status == status).ToList();
    }
    public async Task<WorkItem> AddAsync(string owner, string title, WorkStatus status, CancellationToken ct = default)
    {
        var t = Normalize(title);
        var item = new WorkItem(Guid.NewGuid(), owner, t, status, 1);
        await repository.AddAsync(item, ct);
        return item;
    }
    public async Task<WorkItem> UpdateAsync(string owner, Guid id, string title, WorkStatus status, int expectedVersion, CancellationToken ct = default)
    {
        if (expectedVersion <= 0) throw new ArgumentException("Expected version must be positive.");
        var t = Normalize(title);
        var existing = await repository.FindAsync(id, ct);
        if (existing is null || existing.Owner != owner) throw new ItemNotFoundException();
        var replacement = existing with { Title = t, Status = status, Version = existing.Version + 1 };
        if (!await repository.ReplaceAsync(replacement, expectedVersion, ct)) throw new VersionConflictException();
        return replacement;
    }
    public async Task DeleteAsync(string owner, Guid id, int expectedVersion, CancellationToken ct = default)
    {
        if (expectedVersion <= 0) throw new ArgumentException("Expected version must be positive.");
        var existing = await repository.FindAsync(id, ct);
        if (existing is null || existing.Owner != owner) throw new ItemNotFoundException();
        if (!await repository.DeleteAsync(id, owner, expectedVersion, ct)) throw new VersionConflictException();
    }
    private static string Normalize(string title)
    {
        var t = title?.Trim() ?? string.Empty;
        if (t.Length < 1 || t.Length > 120) throw new ArgumentException("Title must be 1-120 characters.");
        return t;
    }
}
