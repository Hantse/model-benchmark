using WorkBoard.Application;
using WorkBoard.Domain;

namespace WorkBoard.Infrastructure;

public sealed class InMemoryWorkItemRepository : IWorkItemRepository
{
    // Implement isolated thread-safe storage and atomic compare-and-swap operations.
    public IReadOnlyList<WorkItem> List(string owner, WorkStatus? status = null) => [];
    public WorkItem? Find(string owner, Guid id) => null;
    public void Add(WorkItem item) { }
    public MutationResult TryUpdate(string owner, Guid id, int expectedVersion, string title, WorkStatus status) => new(MutationStatus.NotFound);
    public MutationResult TryDelete(string owner, Guid id, int expectedVersion) => new(MutationStatus.NotFound);
}
