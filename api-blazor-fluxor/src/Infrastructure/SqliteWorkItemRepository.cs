using Benchmark.Domain;

namespace Benchmark.Infrastructure;

// Implement real SQLite persistence and SQL-level compare-and-swap.
// Separate repository instances using this connection string must share durable data.
public sealed class SqliteWorkItemRepository(string connectionString) : IWorkItemRepository
{
    public Task InitializeAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<WorkItem>> ListAsync(string owner, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<WorkItem?> FindAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddAsync(WorkItem item, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ReplaceAsync(WorkItem replacement, int expectedVersion, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> DeleteAsync(Guid id, string owner, int expectedVersion, CancellationToken ct = default) => throw new NotImplementedException();
}
