using Benchmark.Application;
using Benchmark.Domain;
namespace Benchmark.Infrastructure;
public sealed class InMemoryWorkItemRepository : IWorkItemRepository
{
    private readonly object gate = new();
    private readonly List<WorkItem> items = [];
    public Task<IReadOnlyList<WorkItem>> ListAsync(string owner, CancellationToken ct = default) { lock (gate) return Task.FromResult<IReadOnlyList<WorkItem>>(items.Where(i => i.Owner == owner).ToArray()); }
    public Task<WorkItem?> FindAsync(Guid id, CancellationToken ct = default) { lock (gate) return Task.FromResult(items.FirstOrDefault(i => i.Id == id)); }
    public Task AddAsync(WorkItem item, CancellationToken ct = default) { lock (gate) items.Add(item); return Task.CompletedTask; }
    public Task<bool> ReplaceAsync(WorkItem replacement, int expectedVersion, CancellationToken ct = default) { lock (gate) { var index = items.FindIndex(i => i.Id == replacement.Id && i.Owner == replacement.Owner && i.Version == expectedVersion); if (index < 0) return Task.FromResult(false); items[index] = replacement; return Task.FromResult(true); } }
    public Task<bool> DeleteAsync(Guid id, string owner, int expectedVersion, CancellationToken ct = default) { lock (gate) return Task.FromResult(items.RemoveAll(i => i.Id == id && i.Owner == owner && i.Version == expectedVersion) == 1); }
}
