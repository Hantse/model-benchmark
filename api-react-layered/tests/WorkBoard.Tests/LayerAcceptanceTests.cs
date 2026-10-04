using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkBoard.Application;
using WorkBoard.Domain;
using WorkBoard.Infrastructure;
using Xunit;

namespace WorkBoard.Tests;

public sealed class LayerAcceptanceTests
{
    [Fact] public void Domain_has_no_outward_project_or_web_dependencies() { var refs = typeof(WorkItem).Assembly.GetReferencedAssemblies().Select(x => x.Name!).ToArray(); Assert.DoesNotContain(refs, x => x.StartsWith("WorkBoard.") || x.StartsWith("Microsoft.AspNetCore") || x.StartsWith("Microsoft.EntityFrameworkCore")); }
    [Fact] public void Application_depends_on_domain_and_not_infrastructure_or_web() { var refs = typeof(WorkItemService).Assembly.GetReferencedAssemblies().Select(x => x.Name!).ToArray(); Assert.Contains("WorkBoard.Domain", refs); Assert.DoesNotContain(refs, x => x is "WorkBoard.Infrastructure" or "WorkBoard.Api" || x.StartsWith("Microsoft.AspNetCore")); }
    [Fact] public void Infrastructure_depends_inwards_and_not_on_API() { var refs = typeof(InMemoryWorkItemRepository).Assembly.GetReferencedAssemblies().Select(x => x.Name!).ToArray(); Assert.Contains("WorkBoard.Application", refs); Assert.DoesNotContain("WorkBoard.Api", refs); }
    [Fact] public void DI_wires_application_to_repository_interface() { using var api = new ApiFixture(); using var scope = api.Services.CreateScope(); var repository = scope.ServiceProvider.GetRequiredService<IWorkItemRepository>(); var service = scope.ServiceProvider.GetRequiredService<WorkItemService>(); Assert.IsType<InMemoryWorkItemRepository>(repository); Assert.Same(repository, service.Repository); }
    [Fact] public void Service_create_uses_injected_repository_and_domain_rules() { var repo = new RecordingRepository(); var service = new WorkItemService(repo); var r = service.Create("alice@example.test", "  Ship  ", "InProgress"); Assert.Equal(201, r.StatusCode); Assert.NotNull(r.Item); Assert.Equal("alice@example.test", r.Item.Owner); Assert.Equal("Ship", r.Item.Title); Assert.Equal(WorkStatus.InProgress, r.Item.Status); Assert.Equal(1, r.Item.Version); Assert.NotEqual(Guid.Empty, r.Item.Id); Assert.Equal(r.Item, Assert.Single(repo.Added)); }
    [Fact] public void Service_invalid_create_does_not_touch_repository() { var repo = new RecordingRepository(); var service = new WorkItemService(repo); Assert.Equal(400, service.Create("alice", " ", "Todo").StatusCode); Assert.Equal(400, service.Create("alice", new string('x', 121), "Todo").StatusCode); Assert.Equal(400, service.Create("alice", "x", "Invalid").StatusCode); Assert.Empty(repo.Added); }
    [Fact] public void Service_list_passes_owner_and_parsed_filter_to_repository() { var repo = new RecordingRepository(); var service = new WorkItemService(repo); var r = service.List("alice", "Done"); Assert.Equal(200, r.StatusCode); Assert.Equal(("alice", (WorkStatus?)WorkStatus.Done), repo.ListCall); Assert.Equal(repo.ListItems, r.Items); Assert.Equal(400, service.List("alice", "Invalid").StatusCode); }
    [Fact] public void Service_update_uses_repository_compare_and_swap() { var repo = new RecordingRepository(); var id = Guid.NewGuid(); var expected = new WorkItem(id, "alice", "Done", WorkStatus.Done, 8); repo.NextMutation = new(MutationStatus.Success, expected); var r = new WorkItemService(repo).Update("alice", id, "  Done  ", "Done", 7); Assert.Equal(200, r.StatusCode); Assert.Equal(expected, r.Item); Assert.Equal(("alice", id, 7, "Done", WorkStatus.Done), repo.UpdateCall); }
    [Fact] public void Service_invalid_update_never_calls_repository() { var repo = new RecordingRepository(); var service = new WorkItemService(repo); Assert.Equal(400, service.Update("alice", Guid.NewGuid(), " ", "Todo", 1).StatusCode); Assert.Equal(400, service.Update("alice", Guid.NewGuid(), "x", "Unknown", 1).StatusCode); Assert.Equal(400, service.Update("alice", Guid.NewGuid(), "x", "Todo", 0).StatusCode); Assert.Null(repo.UpdateCall); }
    [Fact] public void Service_maps_missing_and_conflicting_mutations_without_success() { var repo = new RecordingRepository(); var service = new WorkItemService(repo); repo.NextMutation = new(MutationStatus.Conflict); Assert.Equal(409, service.Update("alice", Guid.NewGuid(), "x", "Todo", 1).StatusCode); Assert.Equal(409, service.Delete("alice", Guid.NewGuid(), 1).StatusCode); repo.NextMutation = new(MutationStatus.NotFound); Assert.Equal(404, service.Update("alice", Guid.NewGuid(), "x", "Todo", 1).StatusCode); Assert.Equal(404, service.Delete("alice", Guid.NewGuid(), 1).StatusCode); }
    [Fact] public void Service_delete_passes_owner_and_version_and_validates_input() { var repo = new RecordingRepository { NextMutation = new(MutationStatus.Success) }; var id = Guid.NewGuid(); var service = new WorkItemService(repo); Assert.Equal(204, service.Delete("alice", id, 5).StatusCode); Assert.Equal(("alice", id, 5), repo.DeleteCall); repo.DeleteCall = null; Assert.Equal(400, service.Delete("alice", id, 0).StatusCode); Assert.Null(repo.DeleteCall); }
    [Fact] public async Task API_reads_through_injected_application_repository() { var repo = new RecordingRepository(); using var api = new InjectedFixture(repo); using var alice = await api.Login(); var rows = await alice.GetFromJsonAsync<WorkBoard.Api.WorkItemDto[]>("/api/work-items?status=Done"); Assert.Equal(("alice@example.test", (WorkStatus?)WorkStatus.Done), repo.ListCall); Assert.Equal(repo.ListItems[0].Id, Assert.Single(rows!).Id); }
    [Fact] public void Repository_compare_and_swap_is_atomic_and_owner_scoped() { var repo = new InMemoryWorkItemRepository(); var id = Guid.NewGuid(); var item = new WorkItem(id, "alice", "Original", WorkStatus.Todo, 1); repo.Add(item); Assert.Empty(repo.List("bob")); Assert.Null(repo.Find("bob", id)); Assert.Equal(MutationStatus.NotFound, repo.TryUpdate("bob", id, 1, "Stolen", WorkStatus.Done).Status); var results = new MutationResult[16]; Parallel.For(0, results.Length, i => results[i] = repo.TryUpdate("alice", id, 1, $"Winner {i}", WorkStatus.InProgress)); Assert.Single(results.Where(x => x.Status == MutationStatus.Success)); Assert.Equal(15, results.Count(x => x.Status == MutationStatus.Conflict)); Assert.Equal(2, repo.Find("alice", id)!.Version); Assert.Equal(MutationStatus.Conflict, repo.TryDelete("alice", id, 1).Status); Assert.Equal(MutationStatus.Success, repo.TryDelete("alice", id, 2).Status); Assert.Null(repo.Find("alice", id)); }

    private sealed class InjectedFixture(RecordingRepository repository) : ApiFixture
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) { base.ConfigureWebHost(builder); builder.ConfigureServices(services => { services.RemoveAll<IWorkItemRepository>(); services.AddSingleton<IWorkItemRepository>(repository); }); }
    }
    private sealed class RecordingRepository : IWorkItemRepository
    {
        public List<WorkItem> Added { get; } = [];
        public IReadOnlyList<WorkItem> ListItems { get; } = [new(Guid.NewGuid(), "alice@example.test", "Injected item", WorkStatus.Done, 3)];
        public (string, WorkStatus?)? ListCall;
        public (string, Guid, int, string, WorkStatus)? UpdateCall;
        public (string, Guid, int)? DeleteCall;
        public MutationResult NextMutation { get; set; } = new(MutationStatus.NotFound);
        public IReadOnlyList<WorkItem> List(string owner, WorkStatus? status = null) { ListCall = (owner, status); return ListItems; }
        public WorkItem? Find(string owner, Guid id) => null;
        public void Add(WorkItem item) => Added.Add(item);
        public MutationResult TryUpdate(string owner, Guid id, int expectedVersion, string title, WorkStatus status) { UpdateCall = (owner, id, expectedVersion, title, status); return NextMutation; }
        public MutationResult TryDelete(string owner, Guid id, int expectedVersion) { DeleteCall = (owner, id, expectedVersion); return NextMutation; }
    }
}
