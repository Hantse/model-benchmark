using Benchmark.Domain;
namespace Benchmark.Application;
public interface IWorkItemRepository
{
    Task<IReadOnlyList<WorkItem>> ListAsync(string owner, CancellationToken ct = default);
    Task<WorkItem?> FindAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(WorkItem item, CancellationToken ct = default);
    Task<bool> ReplaceAsync(WorkItem replacement, int expectedVersion, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, string owner, int expectedVersion, CancellationToken ct = default);
}
public sealed class VersionConflictException : Exception { public VersionConflictException() : base("The item changed. Reload it before retrying.") { } }
public sealed class ItemNotFoundException : Exception { public ItemNotFoundException() : base("Work item not found.") { } }
