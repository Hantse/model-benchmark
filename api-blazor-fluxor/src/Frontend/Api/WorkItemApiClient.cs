using System.Collections.Immutable;
using System.Net.Http.Json;
using System.Text.Json;
namespace Benchmark.Frontend.Api;
public sealed class WorkItemApiClient : IWorkItemApi
{
    private readonly HttpClient http;
    public WorkItemApiClient(HttpClient http) { this.http = http; }
    private static HttpRequestMessage Request(string method, string url, string? token) { var request = new HttpRequestMessage(new HttpMethod(method), url); if (token is not null) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token); return request; }
    private async Task<T> ReadAsync<T>(HttpRequestMessage request, Func<HttpResponseMessage, Task<T>> success) { using var response = await http.SendAsync(request); if (response.IsSuccessStatusCode) return await success(response); throw new ApiFailureException((int)response.StatusCode, await response.Content.ReadAsStringAsync()); }
    public Task<string> LoginAsync(string email, string password) => ReadAsync<string>(Request("POST", "/api/auth/login", null), async r => (await r.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken);
    public Task<ImmutableArray<WorkItemView>> ListAsync(string token) => ReadAsync<ImmutableArray<WorkItemView>>(Request("GET", "/api/work-items", token), async r => (await r.Content.ReadFromJsonAsync<WorkItemView[]>())!.ToImmutableArray());
    public Task<WorkItemView> AddAsync(string token, string title, string status) => ReadAsync<WorkItemView>(Request("POST", "/api/work-items", token), async r => await r.Content.ReadFromJsonAsync<WorkItemView>()!);
    public Task<WorkItemView> UpdateAsync(string token, Guid id, string title, string status, int expectedVersion) => ReadAsync<WorkItemView>(Request("PUT", $"/api/work-items/{id}", token), async r => await r.Content.ReadFromJsonAsync<WorkItemView>()!);
    public Task DeleteAsync(string token, Guid id, int expectedVersion) => ReadAsync<object?>(Request("DELETE", $"/api/work-items/{id}?expectedVersion={expectedVersion}", token), _ => Task.FromResult<object?>(null));
    private sealed record LoginResponse(string AccessToken);
}
