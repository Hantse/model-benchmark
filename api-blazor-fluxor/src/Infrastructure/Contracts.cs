using Benchmark.Domain;

namespace Benchmark.Infrastructure;

public interface IWorkItemRepository
{
    Task<IReadOnlyList<WorkItem>> ListAsync(string owner, CancellationToken ct = default);
    Task<WorkItem?> FindAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(WorkItem item, CancellationToken ct = default);
    Task<bool> ReplaceAsync(WorkItem replacement, int expectedVersion, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, string owner, int expectedVersion, CancellationToken ct = default);
}

public interface IWorkItemService
{
    Task<IReadOnlyList<WorkItem>> ListAsync(string owner, WorkStatus? status = null, CancellationToken ct = default);
    Task<WorkItem> AddAsync(string owner, string title, WorkStatus status, CancellationToken ct = default);
    Task<WorkItem> UpdateAsync(string owner, Guid id, string title, WorkStatus status, int expectedVersion, CancellationToken ct = default);
    Task DeleteAsync(string owner, Guid id, int expectedVersion, CancellationToken ct = default);
}
