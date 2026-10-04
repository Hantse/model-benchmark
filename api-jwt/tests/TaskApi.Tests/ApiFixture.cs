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
using TaskApi;
using Xunit;

namespace TaskApi.Tests;

public sealed class ApiFixture : WebApplicationFactory<Program>
{
    public const string Key = "benchmark-only-synthetic-signing-key-64-characters-long-0123456789";
    public const string Issuer = "benchmark-task-api";
    public const string Audience = "benchmark-task-client";
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
        ["Jwt:SigningKey"] = Key, ["Jwt:Issuer"] = Issuer, ["Jwt:Audience"] = Audience
    }));
    public async Task<HttpClient> Login(string email = "alice@example.test", string password = "Alice!234")
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }
    public static async Task<TaskDto> Create(HttpClient client, string title = "Write tests")
    {
        var response = await client.PostAsJsonAsync("/api/tasks", new TaskInput(title));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskDto>();
        Assert.NotNull(task);
        return task;
    }
    public static string SignedToken(DateTime expires, string key = Key, string issuer = Issuer, string audience = Audience) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        issuer, audience, [new Claim(JwtRegisteredClaimNames.Sub, "alice@example.test"), new Claim(ClaimTypes.Role, "User")],
        notBefore: expires.AddHours(-1), expires: expires,
        signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)));
}
