using Benchmark.Application;
using Benchmark.Domain;
namespace Benchmark.Infrastructure;
public sealed class InMemoryWorkItemRepository : IWorkItemRepository
{
    private readonly object _gate = new();
    private readonly List<WorkItem> _items = [];
    public Task<IReadOnlyList<WorkItem>> ListAsync(string owner, CancellationToken ct = default)
    {
        lock (_gate) return Task.FromResult<IReadOnlyList<WorkItem>>(_items.Where(i => i.Owner == owner).ToArray());
    }
    public Task<WorkItem?> FindAsync(Guid id, CancellationToken ct = default)
    {
        lock (_gate) return Task.FromResult(_items.FirstOrDefault(i => i.Id == id));
    }
    public Task AddAsync(WorkItem item, CancellationToken ct = default)
    {
        lock (_gate) _items.Add(item);
        return Task.CompletedTask;
    }
    public Task<bool> ReplaceAsync(WorkItem replacement, int expectedVersion, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var index = _items.FindIndex(i => i.Id == replacement.Id && i.Owner == replacement.Owner && i.Version == expectedVersion);
            if (index < 0) return Task.FromResult(false);
            _items[index] = replacement;
            return Task.FromResult(true);
        }
    }
    public Task<bool> DeleteAsync(Guid id, string owner, int expectedVersion, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var removed = _items.RemoveAll(i => i.Id == id && i.Owner == owner && i.Version == expectedVersion) == 1;
            return Task.FromResult(removed);
        }
    }
}
