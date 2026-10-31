using Benchmark.Application;
using Benchmark.Domain;
namespace Benchmark.Infrastructure;
// Process-local persistence with atomic compare-and-swap.
public sealed class InMemoryWorkItemRepository : IWorkItemRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, WorkItem> _items = new();

    public Task<IReadOnlyList<WorkItem>> ListAsync(string owner, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            IReadOnlyList<WorkItem> result = _items.Values
                .Where(i => i.Owner == owner)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task<WorkItem?> FindAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _items.TryGetValue(id, out var item);
            return Task.FromResult(item);
        }
    }

    public Task AddAsync(WorkItem item, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _items[item.Id] = item;
        }
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceAsync(WorkItem replacement, int expectedVersion, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_items.TryGetValue(replacement.Id, out var current))
                return Task.FromResult(false);
            if (current.Version != expectedVersion)
                return Task.FromResult(false);
            _items[replacement.Id] = replacement;
            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteAsync(Guid id, string owner, int expectedVersion, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_items.TryGetValue(id, out var current))
                return Task.FromResult(false);
            if (current.Owner != owner)
                return Task.FromResult(false);
            if (current.Version != expectedVersion)
                return Task.FromResult(false);
            _items.Remove(id);
            return Task.FromResult(true);
        }
    }
}
