using Benchmark.Domain;
namespace Benchmark.Application;
public sealed class WorkItemService(IWorkItemRepository repository)
{
    public async Task<IReadOnlyList<WorkItem>> ListAsync(string owner, WorkStatus? status = null, CancellationToken ct = default)
    {
        var items = await repository.ListAsync(owner, ct);
        return status is null ? items : items.Where(i => i.Status == status).ToArray();
    }

    public async Task<WorkItem> AddAsync(string owner, string title, WorkStatus status, CancellationToken ct = default)
    {
        var trimmed = NormalizeTitle(title);
        var item = new WorkItem(Guid.NewGuid(), owner, trimmed, status, 1);
        await repository.AddAsync(item, ct);
        return item;
    }

    public async Task<WorkItem> UpdateAsync(string owner, Guid id, string title, WorkStatus status, int expectedVersion, CancellationToken ct = default)
    {
        var trimmed = NormalizeTitle(title);
        if (expectedVersion <= 0) throw new ArgumentException("Expected version must be positive.", nameof(expectedVersion));
        var existing = await repository.FindAsync(id, ct);
        if (existing is null || existing.Owner != owner) throw new ItemNotFoundException();
        var replacement = existing with { Title = trimmed, Status = status, Version = existing.Version + 1 };
        var ok = await repository.ReplaceAsync(replacement, expectedVersion, ct);
        if (!ok) throw new VersionConflictException();
        return replacement;
    }

    public async Task DeleteAsync(string owner, Guid id, int expectedVersion, CancellationToken ct = default)
    {
        if (expectedVersion <= 0) throw new ArgumentException("Expected version must be positive.", nameof(expectedVersion));
        var existing = await repository.FindAsync(id, ct);
        if (existing is null || existing.Owner != owner) throw new ItemNotFoundException();
        var ok = await repository.DeleteAsync(id, owner, expectedVersion, ct);
        if (!ok) throw new VersionConflictException();
    }

    private static string NormalizeTitle(string title)
    {
        var trimmed = title?.Trim() ?? string.Empty;
        if (trimmed.Length < 1 || trimmed.Length > 120) throw new ArgumentException("Title must be 1 to 120 characters after trimming.", nameof(title));
        return trimmed;
    }
}
