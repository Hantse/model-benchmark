using System.Collections.Immutable;
using Benchmark.Frontend.Api;
namespace Benchmark.Frontend.Tests;
public sealed record ApiCall(string Method, string? Token, Guid? Id=null, string? Title=null, string? Status=null, int? ExpectedVersion=null);
public sealed class FakeApi : IWorkItemApi
{
    public List<ApiCall> Calls {get;}=[];
    public ImmutableArray<WorkItemView> Items {get;set;}=[];
    public TaskCompletionSource<string>? LoginGate;
    public TaskCompletionSource<ImmutableArray<WorkItemView>>? ListGate;
    public TaskCompletionSource<WorkItemView>? AddGate;
    public int? FailureStatus;
    public async Task<string> LoginAsync(string email,string password){Calls.Add(new("LOGIN",null,Title:email));if(LoginGate is not null)return await LoginGate.Task;if(email!="alice@example.test"||password!="Alice!234")throw new ApiFailureException(401,"Invalid credentials");return "test-token";}
    public async Task<ImmutableArray<WorkItemView>> ListAsync(string token){Calls.Add(new("GET",token));if(FailureStatus is{} f)throw new ApiFailureException(f,"API failure");return ListGate is null?Items:await ListGate.Task;}
    public async Task<WorkItemView> AddAsync(string token,string title,string status){Calls.Add(new("POST",token,Title:title,Status:status));if(FailureStatus is{} f)throw new ApiFailureException(f,"API failure");if(AddGate is not null)return await AddGate.Task;var item=new WorkItemView(Guid.NewGuid(),title.Trim(),status,1);Items=Items.Add(item);return item;}
    public Task<WorkItemView> UpdateAsync(string token,Guid id,string title,string status,int expectedVersion){Calls.Add(new("PUT",token,id,title,status,expectedVersion));if(FailureStatus is{} f)throw new ApiFailureException(f,"API failure");var before=Items.Single(t=>t.Id==id);if(before.Version!=expectedVersion)throw new ApiFailureException(409,"Version conflict");var item=before with{Title=title.Trim(),Status=status,Version=expectedVersion+1};Items=Items.Replace(before,item);return Task.FromResult(item);}
    public Task DeleteAsync(string token,Guid id,int expectedVersion){Calls.Add(new("DELETE",token,id,ExpectedVersion:expectedVersion));if(FailureStatus is{} f)throw new ApiFailureException(f,"API failure");var before=Items.Single(t=>t.Id==id);if(before.Version!=expectedVersion)throw new ApiFailureException(409,"Version conflict");Items=Items.Remove(before);return Task.CompletedTask;}
}
