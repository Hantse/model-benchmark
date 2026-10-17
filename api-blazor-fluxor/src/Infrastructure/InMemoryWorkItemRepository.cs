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
            IReadOnlyList<WorkItem> snapshot = _items.Values.Where(i => i.Owner == owner).ToArray();
            return Task.FromResult(snapshot);
        }
    }

    public Task<WorkItem?> FindAsync(Guid id, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _items.TryGetValue(id, out var item);
            return Task.FromResult(item);
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

    public Task<bool> ReplaceAsync(WorkItem replacement, int expectedVersion, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_items.TryGetValue(replacement.Id, out var current) && current.Owner == replacement.Owner && current.Version == expectedVersion)
            {
                _items[replacement.Id] = replacement;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    public Task<bool> DeleteAsync(Guid id, string owner, int expectedVersion, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_items.TryGetValue(id, out var current) && current.Owner == owner && current.Version == expectedVersion)
            {
                _items.Remove(id);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }
}
