using Benchmark.Application;
using Benchmark.Domain;
namespace Benchmark.Infrastructure;
// Process-local persistence; atomic compare-and-swap is deliberately missing.
public sealed class InMemoryWorkItemRepository : IWorkItemRepository
{
    public Task<IReadOnlyList<WorkItem>> ListAsync(string owner, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<WorkItem?> FindAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddAsync(WorkItem item, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ReplaceAsync(WorkItem replacement, int expectedVersion, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> DeleteAsync(Guid id, string owner, int expectedVersion, CancellationToken ct = default) => throw new NotImplementedException();
}
