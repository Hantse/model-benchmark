using Benchmark.Application;
using Benchmark.Domain;
namespace Benchmark.Infrastructure;
public sealed class InMemoryWorkItemRepository : IWorkItemRepository
{
    private readonly object sync = new();
    private readonly Dictionary<Guid, WorkItem> items = new();
    public Task<IReadOnlyList<WorkItem>> ListAsync(string owner, CancellationToken ct = default)
    {
        lock (sync)
        {
            return Task.FromResult<IReadOnlyList<WorkItem>>(items.Values.Where(item => item.Owner == owner).ToArray());
        }
    }

    public Task<WorkItem?> FindAsync(Guid id, CancellationToken ct = default)
    {
        lock (sync)
        {
            return Task.FromResult(items.GetValueOrDefault(id));
        }
    }

    public Task AddAsync(WorkItem item, CancellationToken ct = default)
    {
        lock (sync)
        {
            items[item.Id] = item;
        }
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceAsync(WorkItem item, int expectedVersion, CancellationToken ct = default)
    {
        lock (sync)
        {
            if (items.TryGetValue(item.Id, out var existing) && existing.Owner == item.Owner && existing.Version == expectedVersion)
            {
                items[item.Id] = item;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    public Task<bool> DeleteAsync(Guid id, string owner, int expectedVersion, CancellationToken ct = default)
    {
        lock (sync)
        {
            if (items.TryGetValue(id, out var existing) && existing.Owner == owner && existing.Version == expectedVersion)
            {
                items.Remove(id);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }
}
