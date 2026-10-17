using Benchmark.Application;
using Benchmark.Domain;
namespace Benchmark.Infrastructure;
public sealed class InMemoryWorkItemRepository : IWorkItemRepository
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, WorkItem> items = new();
    public Task<IReadOnlyList<WorkItem>> ListAsync(string owner, CancellationToken ct = default)
    {
        lock (gate) return Task.FromResult<IReadOnlyList<WorkItem>>(items.Values.Where(i => i.Owner == owner).ToArray());
    }
    public Task<WorkItem?> FindAsync(Guid id, CancellationToken ct = default)
    {
        lock (gate) return Task.FromResult(items.TryGetValue(id, out var item) ? item : null);
    }
    public Task AddAsync(WorkItem item, CancellationToken ct = default)
    {
        lock (gate) items[item.Id] = item;
        return Task.CompletedTask;
    }
    public Task<bool> ReplaceAsync(WorkItem replacement, int expectedVersion, CancellationToken ct = default)
    {
        lock (gate)
        {
            if (!items.TryGetValue(replacement.Id, out var current) || current.Owner != replacement.Owner || current.Version != expectedVersion) return Task.FromResult(false);
            items[replacement.Id] = replacement;
            return Task.FromResult(true);
        }
    }
    public Task<bool> DeleteAsync(Guid id, string owner, int expectedVersion, CancellationToken ct = default)
    {
        lock (gate)
        {
            if (!items.TryGetValue(id, out var current) || current.Owner != owner || current.Version != expectedVersion) return Task.FromResult(false);
            items.Remove(id);
            return Task.FromResult(true);
        }
    }
}
