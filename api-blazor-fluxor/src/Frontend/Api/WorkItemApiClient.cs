using System.Collections.Immutable;
namespace Benchmark.Frontend.Api;
// Implement the real HTTP adapter; effects call this abstraction.
public sealed class WorkItemApiClient(HttpClient http) : IWorkItemApi
{
    public Task<string> LoginAsync(string email, string password) => throw new NotImplementedException();
    public Task<ImmutableArray<WorkItemView>> ListAsync(string token) => throw new NotImplementedException();
    public Task<WorkItemView> AddAsync(string token, string title, string status) => throw new NotImplementedException();
    public Task<WorkItemView> UpdateAsync(string token, Guid id, string title, string status, int expectedVersion) => throw new NotImplementedException();
    public Task DeleteAsync(string token, Guid id, int expectedVersion) => throw new NotImplementedException();
}
