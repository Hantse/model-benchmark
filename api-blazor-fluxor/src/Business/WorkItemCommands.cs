using Benchmark.Domain;
using Benchmark.Infrastructure;
using OneOf;
namespace Benchmark.Business;

// Implement CQRS command orchestration and typed expected outcomes, without HTTP concerns.
public sealed class WorkItemCommands(IWorkItemService service) : IWorkItemCommands
{
    public Task<OneOf<WorkItem, ValidationError>> Handle(CreateWorkItem command, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<OneOf<WorkItem, ValidationError, NotFound, Conflict>> Handle(UpdateWorkItem command, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<OneOf<Deleted, ValidationError, NotFound, Conflict>> Handle(DeleteWorkItem command, CancellationToken ct = default) => throw new NotImplementedException();
}
