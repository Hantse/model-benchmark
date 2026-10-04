using Benchmark.Application;
using Benchmark.Domain;
using Benchmark.Infrastructure;
using System.Xml.Linq;
using Xunit;
namespace Benchmark.Tests;
public sealed class LayerTests
{
    private static WorkItem Sample(string owner="alice") => new(Guid.NewGuid(),owner,"Original",WorkStatus.Todo,1);
    [Fact] public void Domain_has_no_application_or_transport_dependencies() {var dependencies=typeof(WorkItem).Assembly.GetReferencedAssemblies();Assert.DoesNotContain(dependencies,d=>d.Name!.StartsWith("Benchmark.")||d.Name.StartsWith("Microsoft.AspNetCore"));}
    [Fact] public void Application_cannot_reference_infrastructure_or_API() {var dependencies=typeof(WorkItemService).Assembly.GetReferencedAssemblies();Assert.DoesNotContain(dependencies,d=>d.Name is "Benchmark.Infrastructure" or "Benchmark.Api" or "Benchmark.Frontend");}
    [Fact] public void Source_project_references_preserve_dependency_direction() {var root=Root();var expected=new Dictionary<string,string[]>{{"Domain",[]},{"Application",["Domain"]},{"Infrastructure",["Application","Domain"]},{"Api",["Application","Infrastructure"]},{"Frontend",[]}};foreach(var pair in expected){var doc=XDocument.Load(Path.Combine(root,"src",pair.Key,pair.Key+".csproj"));var actual=doc.Descendants("ProjectReference").Select(e=>Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value)).Order().ToArray();Assert.Equal(pair.Value.Order(),actual);}}
    [Fact] public async Task Application_add_uses_injected_repository_and_normalizes() {var r=new RecordingRepository();var s=new WorkItemService(r);var item=await s.AddAsync("alice","  New item  ",WorkStatus.Todo);Assert.Equal("New item",item.Title);Assert.Equal("alice",item.Owner);Assert.Equal(1,item.Version);Assert.NotEqual(Guid.Empty,item.Id);Assert.Equal(item,r.Items.Single());}
    [Fact] public async Task Application_validation_precedes_repository_insert() {var r=new RecordingRepository();var s=new WorkItemService(r);await Assert.ThrowsAsync<ArgumentException>(()=>s.AddAsync("alice"," ",WorkStatus.Todo));await Assert.ThrowsAsync<ArgumentException>(()=>s.AddAsync("alice",new string('x',121),WorkStatus.Todo));Assert.Equal(0,r.Writes);}
    [Fact] public async Task Application_list_filters_repository_data() {var r=new RecordingRepository();r.Items.Add(Sample());r.Items.Add(Sample() with{Status=WorkStatus.Done});var s=new WorkItemService(r);Assert.Single(await s.ListAsync("alice",WorkStatus.Done));Assert.Equal(1,r.ListCalls);}
    [Fact] public async Task Application_owner_check_precedes_version_conflict() {var r=new RecordingRepository();var item=Sample();r.Items.Add(item);var s=new WorkItemService(r);await Assert.ThrowsAsync<ItemNotFoundException>(()=>s.UpdateAsync("bob",item.Id,"x",WorkStatus.Done,999));await Assert.ThrowsAsync<ItemNotFoundException>(()=>s.DeleteAsync("bob",item.Id,999));Assert.Equal(0,r.Writes);}
    [Fact] public async Task Application_update_uses_atomic_repository_compare_and_swap() {var r=new RecordingRepository();var item=Sample();r.Items.Add(item);var s=new WorkItemService(r);var next=await s.UpdateAsync("alice",item.Id,"New",WorkStatus.InProgress,1);Assert.Equal(2,next.Version);Assert.Equal(1,r.LastExpectedVersion);Assert.Equal(1,r.Writes);}
    [Fact] public async Task Application_repository_race_is_a_conflict_without_retry() {var r=new RecordingRepository{RejectReplace=true};var item=Sample();r.Items.Add(item);var s=new WorkItemService(r);await Assert.ThrowsAsync<VersionConflictException>(()=>s.UpdateAsync("alice",item.Id,"New",WorkStatus.Done,1));Assert.Equal(1,r.Writes);Assert.Equal(item,r.Items.Single());}
    [Fact] public async Task Application_delete_uses_expected_version() {var r=new RecordingRepository();var item=Sample();r.Items.Add(item);await new WorkItemService(r).DeleteAsync("alice",item.Id,1);Assert.Empty(r.Items);Assert.Equal(1,r.LastExpectedVersion);}
    [Fact] public async Task Repository_list_is_private_and_returns_snapshot() {var r=new InMemoryWorkItemRepository();var a=Sample();await r.AddAsync(a);await r.AddAsync(Sample("bob"));var before=await r.ListAsync("alice");Assert.Equal([a],before);await r.DeleteAsync(a.Id,"alice",1);Assert.Equal([a],before);}
    [Fact] public async Task Repository_compare_and_swap_is_atomic()
    {
        var r=new InMemoryWorkItemRepository();var a=Sample();await r.AddAsync(a);
        const int callers=16;var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var waiting=0;
        var attempts=Enumerable.Range(0,callers).Select(index=>Task.Run(async()=>
        {
            if(Interlocked.Increment(ref waiting)==callers)ready.SetResult();
            await start.Task;
            return await r.ReplaceAsync(a with{Title=$"Contender {index}",Version=2},1);
        })).ToArray();
        try{await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));}finally{start.SetResult();}
        var replies=await Task.WhenAll(attempts);
        Assert.Equal(1,replies.Count(success=>success));Assert.Equal(callers-1,replies.Count(success=>!success));
        var stored=await r.FindAsync(a.Id);Assert.NotNull(stored);Assert.Equal(2,stored.Version);
        var winningIndex=Array.FindIndex(replies,success=>success);Assert.Equal($"Contender {winningIndex}",stored.Title);
    }
    [Fact] public async Task Repository_delete_checks_owner_and_version() {var r=new InMemoryWorkItemRepository();var a=Sample();await r.AddAsync(a);Assert.False(await r.DeleteAsync(a.Id,"bob",1));Assert.False(await r.DeleteAsync(a.Id,"alice",2));Assert.Equal(a,await r.FindAsync(a.Id));Assert.True(await r.DeleteAsync(a.Id,"alice",1));Assert.Null(await r.FindAsync(a.Id));}
    private static string Root(){var p=new DirectoryInfo(AppContext.BaseDirectory);while(p!=null&&!File.Exists(Path.Combine(p.FullName,"Benchmark.slnx")))p=p.Parent;return p?.FullName??throw new Exception("Tester root missing");}
    private sealed class RecordingRepository : IWorkItemRepository
    {
        public List<WorkItem> Items {get;}=[];public int Writes,ListCalls,LastExpectedVersion;public bool RejectReplace;
        public Task<IReadOnlyList<WorkItem>> ListAsync(string owner,CancellationToken ct=default){ListCalls++;return Task.FromResult<IReadOnlyList<WorkItem>>(Items.Where(x=>x.Owner==owner).ToArray());}
        public Task<WorkItem?> FindAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(Items.SingleOrDefault(x=>x.Id==id));
        public Task AddAsync(WorkItem item,CancellationToken ct=default){Writes++;Items.Add(item);return Task.CompletedTask;}
        public Task<bool> ReplaceAsync(WorkItem replacement,int expectedVersion,CancellationToken ct=default){Writes++;LastExpectedVersion=expectedVersion;if(RejectReplace)return Task.FromResult(false);var index=Items.FindIndex(x=>x.Id==replacement.Id&&x.Owner==replacement.Owner&&x.Version==expectedVersion);if(index<0)return Task.FromResult(false);Items[index]=replacement;return Task.FromResult(true);}
        public Task<bool> DeleteAsync(Guid id,string owner,int expectedVersion,CancellationToken ct=default){Writes++;LastExpectedVersion=expectedVersion;return Task.FromResult(Items.RemoveAll(x=>x.Id==id&&x.Owner==owner&&x.Version==expectedVersion)==1);}
    }
}
