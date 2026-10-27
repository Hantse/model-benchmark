using Benchmark.Domain;
using Benchmark.Infrastructure;
using OneOf;
namespace Benchmark.Business;

// Implement CQRS query orchestration through the injected Infrastructure service.
public sealed class WorkItemQueries(IWorkItemService service) : IWorkItemQueries
{
    public Task<OneOf<IReadOnlyList<WorkItem>, ValidationError>> Handle(ListWorkItems query, CancellationToken ct = default) => throw new NotImplementedException();
}
