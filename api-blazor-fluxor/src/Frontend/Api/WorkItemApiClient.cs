using System.Collections.Immutable;
using System.Net.Http.Json;
namespace Benchmark.Frontend.Api;
public sealed class WorkItemApiClient(HttpClient http) : IWorkItemApi
{
    private static HttpRequestMessage Auth(string token, HttpMethod method, string url) { var r = new HttpRequestMessage(method, url); r.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token); return r; }
    private static async Task<T> Read<T>(HttpResponseMessage r) where T : class { if (!r.IsSuccessStatusCode) throw new ApiFailureException((int)r.StatusCode, await r.Content.ReadAsStringAsync()); return await r.Content.ReadFromJsonAsync<T>(); }
    public async Task<string> LoginAsync(string email, string password) { var r = await http.PostAsJsonAsync("/api/auth/login", new { email, password }); if (!r.IsSuccessStatusCode) throw new ApiFailureException((int)r.StatusCode, await r.Content.ReadAsStringAsync()); return (await r.Content.ReadFromJsonAsync<LoginDto>())!.AccessToken; }
    public async Task<ImmutableArray<WorkItemView>> ListAsync(string token) { var r = await http.SendAsync(Auth(token, HttpMethod.Get, "/api/work-items")); var items = await Read<ItemDto[]>(r); return items.Select(i => new WorkItemView(i.Id, i.Title, i.Status, i.Version)).ToImmutableArray(); }
    public async Task<WorkItemView> AddAsync(string token, string title, string status) { var r = await http.SendAsync(Auth(token, HttpMethod.Post, "/api/work-items")); r.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { title, status }), System.Text.Encoding.UTF8, "application/json"); var i = await Read<ItemDto>(r); return new WorkItemView(i.Id, i.Title, i.Status, i.Version); }
    public async Task<WorkItemView> UpdateAsync(string token, Guid id, string title, string status, int expectedVersion) { var r = Auth(token, HttpMethod.Put, $"/api/work-items/{id}"); r.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { title, status, expectedVersion }), System.Text.Encoding.UTF8, "application/json"); var resp = await http.SendAsync(r); var i = await Read<ItemDto>(resp); return new WorkItemView(i.Id, i.Title, i.Status, i.Version); }
    public async Task DeleteAsync(string token, Guid id, int expectedVersion) { var r = await http.SendAsync(Auth(token, HttpMethod.Delete, $"/api/work-items/{id}?expectedVersion={expectedVersion}")); if (!r.IsSuccessStatusCode) throw new ApiFailureException((int)r.StatusCode, await r.Content.ReadAsStringAsync()); }
    private sealed record LoginDto(string AccessToken);
    private sealed record ItemDto(Guid Id, string Title, string Status, int Version);
}
