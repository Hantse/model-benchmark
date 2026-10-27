using Benchmark.Business;
using Benchmark.Domain;
using Benchmark.Infrastructure;
using Xunit;
namespace Benchmark.Tests;

public sealed class CqrsTests
{
    [Fact]
    public async Task Query_forwards_owner_filter_cancellation_and_returns_service_items()
    {
        var service = new RecordingService(); using var cancellation = new CancellationTokenSource();
        var result = await new WorkItemQueries(service).Handle(new("alice", WorkStatus.Done), cancellation.Token);
        Assert.True(result.IsT0); Assert.Same(service.Items, result.AsT0);
        Assert.Equal(("list", "alice", WorkStatus.Done, cancellation.Token), (service.Operation, service.Owner, service.Status, service.Token)); Assert.Equal(1, service.Calls);
    }
    [Fact]
    public async Task Query_passes_an_absent_filter_without_fabricating_one()
    {
        var service = new RecordingService(); await new WorkItemQueries(service).Handle(new("alice"));
        Assert.Null(service.Status); Assert.Equal(1, service.Calls);
    }
    [Fact]
    public async Task Create_forwards_command_and_returns_the_service_entity()
    {
        var service = new RecordingService(); using var cancellation = new CancellationTokenSource();
        var result = await new WorkItemCommands(service).Handle(new CreateWorkItem("alice", "  raw title  ", WorkStatus.InProgress), cancellation.Token);
        Assert.True(result.IsT0); Assert.Equal(service.Item, result.AsT0);
        Assert.Equal(("add", "alice", "  raw title  ", WorkStatus.InProgress, cancellation.Token), (service.Operation, service.Owner, service.Title, service.Status, service.Token)); Assert.Equal(1, service.Calls);
    }
    [Fact]
    public async Task Update_forwards_identity_title_status_version_and_cancellation_once()
    {
        var service = new RecordingService(); using var cancellation = new CancellationTokenSource(); var id = Guid.NewGuid();
        var result = await new WorkItemCommands(service).Handle(new UpdateWorkItem("alice", id, "Updated", WorkStatus.Done, 7), cancellation.Token);
        Assert.True(result.IsT0); Assert.Equal(service.Item, result.AsT0);
        Assert.Equal(("update", "alice", id, "Updated", WorkStatus.Done, 7, cancellation.Token), (service.Operation, service.Owner, service.Id, service.Title, service.Status, service.Version, service.Token)); Assert.Equal(1, service.Calls);
    }
    [Fact]
    public async Task Delete_forwards_identity_version_and_returns_typed_deleted()
    {
        var service = new RecordingService(); using var cancellation = new CancellationTokenSource(); var id = Guid.NewGuid();
        var result = await new WorkItemCommands(service).Handle(new DeleteWorkItem("alice", id, 7), cancellation.Token);
        Assert.True(result.IsT0); Assert.IsType<Deleted>(result.AsT0);
        Assert.Equal(("delete", "alice", id, 7, cancellation.Token), (service.Operation, service.Owner, service.Id, service.Version, service.Token)); Assert.Equal(1, service.Calls);
    }
    [Theory]
    [InlineData("list")][InlineData("add")][InlineData("update")][InlineData("delete")]
    public async Task Expected_validation_is_a_OneOf_error_with_the_service_diagnostic(string operation)
    {
        var service = new RecordingService { Failure = new ArgumentException("Invalid synthetic title") };
        var error = await Validation(operation, service);
        Assert.Equal("Invalid synthetic title", error.Message); Assert.Equal(1, service.Calls);
    }
    [Theory]
    [InlineData("update")][InlineData("delete")]
    public async Task Unknown_or_foreign_item_is_a_typed_not_found(string operation)
    {
        var service = new RecordingService { Failure = new ItemNotFoundException() };
        if (operation == "update") Assert.True((await new WorkItemCommands(service).Handle(new UpdateWorkItem("alice", Guid.NewGuid(), "x", WorkStatus.Todo, 1))).IsT2);
        else Assert.True((await new WorkItemCommands(service).Handle(new DeleteWorkItem("alice", Guid.NewGuid(), 1))).IsT2);
        Assert.Equal(1, service.Calls);
    }
    [Theory]
    [InlineData("update")][InlineData("delete")]
    public async Task Version_conflict_is_a_typed_conflict_without_retry(string operation)
    {
        var service = new RecordingService { Failure = new VersionConflictException() };
        if (operation == "update") Assert.True((await new WorkItemCommands(service).Handle(new UpdateWorkItem("alice", Guid.NewGuid(), "x", WorkStatus.Todo, 1))).IsT3);
        else Assert.True((await new WorkItemCommands(service).Handle(new DeleteWorkItem("alice", Guid.NewGuid(), 1))).IsT3);
        Assert.Equal(1, service.Calls);
    }
    [Theory]
    [InlineData("list")][InlineData("add")][InlineData("update")][InlineData("delete")]
    public async Task Unexpected_failure_is_not_disguised_as_a_business_outcome(string operation)
    {
        var failure = new InvalidOperationException("Storage unavailable"); var service = new RecordingService { Failure = failure };
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => Execute(operation, service))); Assert.Equal(1, service.Calls);
    }
    [Theory]
    [InlineData("list")][InlineData("add")][InlineData("update")][InlineData("delete")]
    public async Task Cancellation_is_not_converted_to_validation_or_retried(string operation)
    {
        var failure = new OperationCanceledException(); var service = new RecordingService { Failure = failure };
        Assert.Same(failure, await Assert.ThrowsAsync<OperationCanceledException>(() => Execute(operation, service))); Assert.Equal(1, service.Calls);
    }
    private static async Task<ValidationError> Validation(string operation, RecordingService service) => operation switch
    {
        "list" => (await new WorkItemQueries(service).Handle(new("alice"))).AsT1,
        "add" => (await new WorkItemCommands(service).Handle(new CreateWorkItem("alice", "x", WorkStatus.Todo))).AsT1,
        "update" => (await new WorkItemCommands(service).Handle(new UpdateWorkItem("alice", Guid.NewGuid(), "x", WorkStatus.Todo, 1))).AsT1,
        _ => (await new WorkItemCommands(service).Handle(new DeleteWorkItem("alice", Guid.NewGuid(), 1))).AsT1
    };
    private static async Task Execute(string operation, RecordingService service)
    {
        if (operation == "list") await new WorkItemQueries(service).Handle(new("alice"));
        else if (operation == "add") await new WorkItemCommands(service).Handle(new CreateWorkItem("alice", "x", WorkStatus.Todo));
        else if (operation == "update") await new WorkItemCommands(service).Handle(new UpdateWorkItem("alice", Guid.NewGuid(), "x", WorkStatus.Todo, 1));
        else await new WorkItemCommands(service).Handle(new DeleteWorkItem("alice", Guid.NewGuid(), 1));
    }
    private sealed class RecordingService : IWorkItemService
    {
        public readonly WorkItem Item = new(Guid.NewGuid(), "alice", "Service result", WorkStatus.Todo, 3);
        public IReadOnlyList<WorkItem> Items { get; } = [new(Guid.NewGuid(), "alice", "Listed", WorkStatus.Done, 4)];
        public Exception? Failure; public string? Operation, Owner, Title; public Guid Id; public WorkStatus? Status; public int Version, Calls; public CancellationToken Token;
        private void Record(string operation, string owner, CancellationToken token) { Operation = operation; Owner = owner; Token = token; Calls++; if (Failure is not null) throw Failure; }
        public Task<IReadOnlyList<WorkItem>> ListAsync(string owner, WorkStatus? status = null, CancellationToken ct = default) { Status = status; Record("list", owner, ct); return Task.FromResult(Items); }
        public Task<WorkItem> AddAsync(string owner, string title, WorkStatus status, CancellationToken ct = default) { Title = title; Status = status; Record("add", owner, ct); return Task.FromResult(Item); }
        public Task<WorkItem> UpdateAsync(string owner, Guid id, string title, WorkStatus status, int expectedVersion, CancellationToken ct = default) { Id = id; Title = title; Status = status; Version = expectedVersion; Record("update", owner, ct); return Task.FromResult(Item); }
        public Task DeleteAsync(string owner, Guid id, int expectedVersion, CancellationToken ct = default) { Id = id; Version = expectedVersion; Record("delete", owner, ct); return Task.CompletedTask; }
    }
}
