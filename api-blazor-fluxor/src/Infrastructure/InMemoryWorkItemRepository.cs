using Benchmark.Application;
using Benchmark.Domain;
namespace Benchmark.Infrastructure;
public sealed class InMemoryWorkItemRepository : IWorkItemRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, WorkItem> _items = new();
    public Task<IReadOnlyList<WorkItem>> ListAsync(string owner, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<WorkItem>>(_items.Values.Where(x => x.Owner == owner).ToArray());
        }
    }
    public Task<WorkItem?> FindAsync(Guid id, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_items.GetValueOrDefault(id));
        }
    }
    public Task AddAsync(WorkItem item, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _items[item.Id] = item;
        }
        return Task.CompletedTask;
    }
    public Task<bool> ReplaceAsync(WorkItem item, int expectedVersion, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(item.Id, out var existing) || existing.Owner != item.Owner || existing.Version != expectedVersion) return Task.FromResult(false);
            _items[item.Id] = item;
            return Task.FromResult(true);
        }
    }
    public Task<bool> DeleteAsync(Guid id, string owner, int expectedVersion, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(id, out var existing) || existing.Owner != owner || existing.Version != expectedVersion) return Task.FromResult(false);
            _items.Remove(id);
            return Task.FromResult(true);
        }
    }
}
