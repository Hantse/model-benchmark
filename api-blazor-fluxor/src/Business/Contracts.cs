using Benchmark.Domain;
using OneOf;
namespace Benchmark.Business;

public sealed record ValidationError(string Message);
public sealed record NotFound;
public sealed record Conflict;
public sealed record Deleted;

public sealed record ListWorkItems(string Owner, WorkStatus? Status = null);
public sealed record CreateWorkItem(string Owner, string Title, WorkStatus Status);
public sealed record UpdateWorkItem(string Owner, Guid Id, string Title, WorkStatus Status, int ExpectedVersion);
public sealed record DeleteWorkItem(string Owner, Guid Id, int ExpectedVersion);

public interface IWorkItemQueries
{
    Task<OneOf<IReadOnlyList<WorkItem>, ValidationError>> Handle(ListWorkItems query, CancellationToken ct = default);
}

public interface IWorkItemCommands
{
    Task<OneOf<WorkItem, ValidationError>> Handle(CreateWorkItem command, CancellationToken ct = default);
    Task<OneOf<WorkItem, ValidationError, NotFound, Conflict>> Handle(UpdateWorkItem command, CancellationToken ct = default);
    Task<OneOf<Deleted, ValidationError, NotFound, Conflict>> Handle(DeleteWorkItem command, CancellationToken ct = default);
}
