using Benchmark.Domain;
namespace Benchmark.Application;
// Replace the stubs while retaining the repository abstraction and public contract.
public sealed class WorkItemService(IWorkItemRepository repository)
{
    public Task<IReadOnlyList<WorkItem>> ListAsync(string owner, WorkStatus? status = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<WorkItem> AddAsync(string owner, string title, WorkStatus status, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<WorkItem> UpdateAsync(string owner, Guid id, string title, WorkStatus status, int expectedVersion, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteAsync(string owner, Guid id, int expectedVersion, CancellationToken ct = default) => throw new NotImplementedException();
}
