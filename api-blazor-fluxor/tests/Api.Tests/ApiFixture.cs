using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Benchmark.Api;
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
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureAppConfiguration((_,config) => config.AddInMemoryCollection(new Dictionary<string,string?> { ["Jwt:SigningKey"]=Key,["Jwt:Issuer"]=Issuer,["Jwt:Audience"]=Audience }));
    public async Task<HttpClient> Login(string email="alice@example.test",string password="Alice!234") { var c=CreateClient(); var r=await c.PostAsJsonAsync("/api/auth/login",new LoginInput(email,password));Assert.Equal(HttpStatusCode.OK,r.StatusCode); var token=await r.Content.ReadFromJsonAsync<LoginResult>();Assert.NotNull(token);c.DefaultRequestHeaders.Authorization=new("Bearer",token.AccessToken);return c; }
    public static async Task<ItemDto> Add(HttpClient c,string title="Build board",string status="Todo") {var r=await c.PostAsJsonAsync("/api/work-items",new CreateInput(title,status));Assert.Equal(HttpStatusCode.Created,r.StatusCode);return (await r.Content.ReadFromJsonAsync<ItemDto>())!;}
    public static string Token(string key=Key,string issuer=Issuer,string audience=Audience,int expiredMinutes=15) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer,audience,[new Claim(JwtRegisteredClaimNames.Sub,"alice@example.test")],notBefore:DateTime.UtcNow.AddHours(-1),expires:DateTime.UtcNow.AddMinutes(expiredMinutes),signingCredentials:new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),SecurityAlgorithms.HmacSha256)));
}
