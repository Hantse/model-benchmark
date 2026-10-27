using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Benchmark.Host;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;
namespace Benchmark.Tests;

public sealed class ApiFixture : WebApplicationFactory<Program>
{
    public const string Key = "benchmark-only-synthetic-signing-key-64-characters-long-0123456789";
    public const string Issuer = "benchmark-work-api", Audience = "benchmark-work-client";
    private readonly string? ownedDirectory;
    public string ConnectionString { get; }

    public ApiFixture(string? connectionString = null)
    {
        if (connectionString is null)
        {
            ownedDirectory = Path.Combine(Path.GetTempPath(), "benchmark-http-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ownedDirectory);
            connectionString = $"Data Source={Path.Combine(ownedDirectory, "work-items.db")};Pooling=False";
        }
        ConnectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Jwt:SigningKey"] = Key, ["Jwt:Issuer"] = Issuer, ["Jwt:Audience"] = Audience,
        ["ConnectionStrings:WorkItems"] = ConnectionString
    }));

    public async Task<HttpClient> Login(string email = "alice@example.test", string password = "Alice!234")
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginInput(email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(token);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.AccessToken);
        return client;
    }

    public static async Task<ItemDto> Add(HttpClient client, string title = "Build board", string status = "Todo")
    {
        var response = await client.PostAsJsonAsync("/api/work-items", new CreateInput(title, status));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ItemDto>())!;
    }

    public static string Token(string key = Key, string issuer = Issuer, string audience = Audience, int expiredMinutes = 15) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer, audience, [new Claim(JwtRegisteredClaimNames.Sub, "alice@example.test")], notBefore: DateTime.UtcNow.AddHours(-1), expires: DateTime.UtcNow.AddMinutes(expiredMinutes), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && ownedDirectory is not null && Directory.Exists(ownedDirectory)) Directory.Delete(ownedDirectory, recursive: true);
    }
}
