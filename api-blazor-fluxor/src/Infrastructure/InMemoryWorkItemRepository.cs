using System.Collections.Concurrent;
using Benchmark.Application;
using Benchmark.Domain;
namespace Benchmark.Infrastructure;
public sealed class InMemoryWorkItemRepository : IWorkItemRepository
{
    private readonly ConcurrentDictionary<Guid, WorkItem> _items = new();
    private readonly object _gate = new();

    public Task<IReadOnlyList<WorkItem>> ListAsync(string owner, CancellationToken ct = default)
    {
        IReadOnlyList<WorkItem> snapshot;
        lock (_gate)
        {
            snapshot = _items.Values.Where(i => i.Owner == owner).ToList();
        }
        return Task.FromResult(snapshot);
    }

    public Task<WorkItem?> FindAsync(Guid id, CancellationToken ct = default)
    {
        WorkItem? item;
        lock (_gate)
        {
            _items.TryGetValue(id, out item);
        }
        return Task.FromResult(item);
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
        bool ok;
        lock (_gate)
        {
            if (_items.TryGetValue(replacement.Id, out var current) && current.Owner == replacement.Owner && current.Version == expectedVersion)
            {
                _items[replacement.Id] = replacement;
                ok = true;
            }
            else ok = false;
        }
        return Task.FromResult(ok);
    }

    public Task<bool> DeleteAsync(Guid id, string owner, int expectedVersion, CancellationToken ct = default)
    {
        bool ok;
        lock (_gate)
        {
            if (_items.TryGetValue(id, out var current) && current.Owner == owner && current.Version == expectedVersion)
            {
                _items.TryRemove(id, out _);
                ok = true;
            }
            else ok = false;
        }
        return Task.FromResult(ok);
    }
}
