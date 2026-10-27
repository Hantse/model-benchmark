using Benchmark.Domain;

namespace Benchmark.Infrastructure;

// Implement validation and ownership through the injected repository.
public sealed class WorkItemService(IWorkItemRepository repository) : IWorkItemService
{
    public Task<IReadOnlyList<WorkItem>> ListAsync(string owner, WorkStatus? status = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<WorkItem> AddAsync(string owner, string title, WorkStatus status, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<WorkItem> UpdateAsync(string owner, Guid id, string title, WorkStatus status, int expectedVersion, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteAsync(string owner, Guid id, int expectedVersion, CancellationToken ct = default) => throw new NotImplementedException();
}
