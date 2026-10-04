using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WorkBoard.Api;
using Xunit;

namespace WorkBoard.Tests;

public class ApiFixture : WebApplicationFactory<Program>
{
    public const string Key = "benchmark-only-synthetic-signing-key-do-not-use-for-production-0123456789";
    public const string Issuer = "benchmark-work-board-api";
    public const string Audience = "benchmark-work-board-client";
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing").ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = Key, ["Jwt:Issuer"] = Issuer, ["Jwt:Audience"] = Audience }));
    public async Task<HttpClient> Login(string email = "alice@example.test", string password = "Alice!234")
    {
        var client = CreateClient(); var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(); Assert.NotNull(login); Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken); return client;
    }
    public static async Task<WorkItemDto> Create(HttpClient client, string title = "Build release", string status = "Todo")
    {
        var response = await client.PostAsJsonAsync("/api/work-items", new CreateWorkItemRequest(title, status));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<WorkItemDto>())!;
    }
    public static string SignedToken(DateTime expires, string? key = null, string? issuer = null, string? audience = null)
    {
        var token = new JwtSecurityToken(issuer ?? Issuer, audience ?? Audience, [new Claim("sub", "alice@example.test"), new Claim("role", "User")], DateTime.UtcNow.AddHours(-1), expires, new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? Key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
