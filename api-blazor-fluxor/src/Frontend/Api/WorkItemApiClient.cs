using System.Collections.Immutable;
using System.Net.Http.Json;
namespace Benchmark.Frontend.Api;
public sealed class WorkItemApiClient(HttpClient http) : IWorkItemApi
{
    private static HttpRequestMessage Auth(string token, HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return req;
    }
    private static HttpRequestMessage Auth(string token, HttpMethod method, string url, HttpContent content)
    {
        var req = new HttpRequestMessage(method, url) { Content = content };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return req;
    }
    private static async Task<T> Read<T>(HttpResponseMessage res) where T : class
    {
        if (!res.IsSuccessStatusCode)
            throw new ApiFailureException((int)res.StatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<T>();
    }
    public async Task<string> LoginAsync(string email, string password)
    {
        var res = await http.PostAsJsonAsync("/api/auth/login", new { email, password });
        if (!res.IsSuccessStatusCode)
            throw new ApiFailureException((int)res.StatusCode, await res.Content.ReadAsStringAsync());
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.AccessToken;
    }
    public async Task<ImmutableArray<WorkItemView>> ListAsync(string token)
    {
        var res = await http.SendAsync(Auth(token, HttpMethod.Get, "/api/work-items"));
        var items = await Read<List<WorkItemView>>(res);
        return items.ToImmutableArray();
    }
    public async Task<WorkItemView> AddAsync(string token, string title, string status)
    {
        var res = await http.SendAsync(Auth(token, HttpMethod.Post, "/api/work-items", new StringContent(System.Text.Json.JsonSerializer.Serialize(new { title, status }), System.Text.Encoding.UTF8, "application/json")));
        return await Read<WorkItemView>(res);
    }
    public async Task<WorkItemView> UpdateAsync(string token, Guid id, string title, string status, int expectedVersion)
    {
        var res = await http.SendAsync(Auth(token, HttpMethod.Put, $"/api/work-items/{id}", new StringContent(System.Text.Json.JsonSerializer.Serialize(new { title, status, expectedVersion }), System.Text.Encoding.UTF8, "application/json")));
        return await Read<WorkItemView>(res);
    }
    public async Task DeleteAsync(string token, Guid id, int expectedVersion)
    {
        var res = await http.SendAsync(Auth(token, HttpMethod.Delete, $"/api/work-items/{id}?expectedVersion={expectedVersion}"));
        if (!res.IsSuccessStatusCode)
            throw new ApiFailureException((int)res.StatusCode, await res.Content.ReadAsStringAsync());
    }
    private sealed record LoginResponse(string AccessToken);
}
