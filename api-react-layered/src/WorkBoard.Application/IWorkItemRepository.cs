using WorkBoard.Domain;

namespace WorkBoard.Application;

public enum MutationStatus { Success, NotFound, Conflict }
public sealed record MutationResult(MutationStatus Status, WorkItem? Item = null);

public interface IWorkItemRepository
{
    IReadOnlyList<WorkItem> List(string owner, WorkStatus? status = null);
    WorkItem? Find(string owner, Guid id);
    void Add(WorkItem item);
    MutationResult TryUpdate(string owner, Guid id, int expectedVersion, string title, WorkStatus status);
    MutationResult TryDelete(string owner, Guid id, int expectedVersion);
}
