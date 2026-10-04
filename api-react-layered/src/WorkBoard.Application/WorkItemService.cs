using WorkBoard.Domain;

namespace WorkBoard.Application;

public sealed record ServiceResult(int StatusCode, WorkItem? Item = null);
public sealed record ListResult(int StatusCode, IReadOnlyList<WorkItem> Items);

public sealed class WorkItemService(IWorkItemRepository repository)
{
    public IWorkItemRepository Repository { get; } = repository;
    // Implement application rules through the injected repository. The stubs are intentional.
    public ListResult List(string owner, string? status = null) => new(501, []);
    public ServiceResult Create(string owner, string? title, string? status) => new(501);
    public ServiceResult Update(string owner, Guid id, string? title, string? status, int expectedVersion) => new(501);
    public ServiceResult Delete(string owner, Guid id, int expectedVersion) => new(501);
}
