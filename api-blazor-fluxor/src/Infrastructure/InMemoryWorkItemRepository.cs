using System.Collections.Concurrent;
using Benchmark.Application;
using Benchmark.Domain;
namespace Benchmark.Infrastructure;
public sealed class InMemoryWorkItemRepository : IWorkItemRepository
{
    private readonly ConcurrentDictionary<Guid, WorkItem> items = new();
    private readonly object mutationGate = new();

    public Task<IReadOnlyList<WorkItem>> ListAsync(string owner, CancellationToken ct = default)
    {
        var result = items.Values.Where(item => item.Owner == owner).ToArray();
        return Task.FromResult<IReadOnlyList<WorkItem>>(result);
    }

    public Task<WorkItem?> FindAsync(Guid id, CancellationToken ct = default)
    {
        items.TryGetValue(id, out var item);
        return Task.FromResult(item);
    }

    public Task AddAsync(WorkItem item, CancellationToken ct = default)
    {
        lock (mutationGate)
        {
            if (items.ContainsKey(item.Id)) throw new InvalidOperationException("Work item already exists.");
            items[item.Id] = item;
        }
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceAsync(WorkItem replacement, int expectedVersion, CancellationToken ct = default)
    {
        lock (mutationGate)
        {
            if (!items.TryGetValue(replacement.Id, out var existing) || existing.Owner != replacement.Owner || existing.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            items[replacement.Id] = replacement;
            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteAsync(Guid id, string owner, int expectedVersion, CancellationToken ct = default)
    {
        lock (mutationGate)
        {
            if (!items.TryGetValue(id, out var existing) || existing.Owner != owner || existing.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            items.TryRemove(id, out _);
            return Task.FromResult(true);
        }
    }
}
