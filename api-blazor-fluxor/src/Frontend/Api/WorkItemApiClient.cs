using System.Collections.Immutable;
using System.Net.Http.Json;
namespace Benchmark.Frontend.Api;
public sealed class WorkItemApiClient(HttpClient http) : IWorkItemApi
{
    private static HttpRequestMessage Request(string method, string url, string? token)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (token is not null) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return request;
    }
    private static async Task<T> ReadAsync<T>(HttpRequestMessage request, CancellationToken ct) where T : class
    {
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new ApiFailureException((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
        return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new ApiFailureException((int)response.StatusCode, "Empty response");
    }
    public async Task<string> LoginAsync(string email, string password)
    {
        var request = Request("POST", "/api/auth/login", null);
        request.Content = JsonContent.Create(new { email, password });
        var result = await ReadAsync<LoginResponse>(request, CancellationToken.None);
        return result.AccessToken;
    }
    public Task<ImmutableArray<WorkItemView>> ListAsync(string token) => ReadAsync<ImmutableArray<WorkItemView>>(Request("GET", "/api/work-items", token), CancellationToken.None);
    public async Task<WorkItemView> AddAsync(string token, string title, string status)
    {
        var request = Request("POST", "/api/work-items", token);
        request.Content = JsonContent.Create(new { title, status });
        return await ReadAsync<WorkItemView>(request, CancellationToken.None);
    }
    public async Task<WorkItemView> UpdateAsync(string token, Guid id, string title, string status, int expectedVersion)
    {
        var request = Request("PUT", $"/api/work-items/{id}", token);
        request.Content = JsonContent.Create(new { title, status, expectedVersion });
        return await ReadAsync<WorkItemView>(request, CancellationToken.None);
    }
    public async Task DeleteAsync(string token, Guid id, int expectedVersion)
    {
        using var response = await http.SendAsync(Request("DELETE", $"/api/work-items/{id}?expectedVersion={expectedVersion}", token), CancellationToken.None);
        if (!response.IsSuccessStatusCode) throw new ApiFailureException((int)response.StatusCode, await response.Content.ReadAsStringAsync(CancellationToken.None));
    }
    private sealed record LoginResponse(string AccessToken);
}
