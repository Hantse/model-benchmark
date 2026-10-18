using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Benchmark.Api;
using Benchmark.Application;
using Benchmark.Domain;
using Benchmark.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
var builder = WebApplication.CreateBuilder(args);
var signingKey = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
var issuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is required.");
var audience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is required.");
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
builder.Services.AddSingleton<IWorkItemRepository, InMemoryWorkItemRepository>();
builder.Services.AddScoped<WorkItemService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});
builder.Services.AddAuthorization();
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/auth/login", (LoginInput input) =>
{
    if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.Password))
        return Results.BadRequest(new { error = "Email and password are required." });
    var account = Accounts.Find(input.Email, input.Password);
    if (account is null) return Results.Unauthorized();
    var now = DateTime.UtcNow;
    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, account.Value.Email),
        new Claim(ClaimTypes.Role, account.Value.Role)
    };
    var token = new JwtSecurityToken(issuer, audience, claims, now, now.AddMinutes(15), new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
    return Results.Ok(new LoginResult(new JwtSecurityTokenHandler().WriteToken(token)));
});
app.MapGet("/api/work-items", async (WorkItemService service, string? status, ClaimsPrincipal user) =>
{
    var owner = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
    if (owner is null) return Results.Unauthorized();
    WorkStatus? filter = null;
    if (status is not null)
    {
        if (!Enum.TryParse<WorkStatus>(status, true, out var parsed)) return Results.BadRequest(new { error = "Invalid status filter." });
        filter = parsed;
    }
    var items = await service.ListAsync(owner, filter);
    return Results.Ok(items.Select(i => new ItemDto(i.Id, i.Title, i.Status.ToString(), i.Version)));
}).RequireAuthorization();
app.MapPost("/api/work-items", async (WorkItemService service, CreateInput input, ClaimsPrincipal user) =>
{
    var owner = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
    if (owner is null) return Results.Unauthorized();
    if (!Enum.TryParse<WorkStatus>(input.Status, true, out var status)) return Results.BadRequest(new { error = "Invalid status." });
    try
    {
        var item = await service.AddAsync(owner, input.Title, status);
        return Results.Created($"/api/work-items/{item.Id}", new ItemDto(item.Id, item.Title, item.Status.ToString(), item.Version));
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).RequireAuthorization();
app.MapPut("/api/work-items/{id:guid}", async (WorkItemService service, Guid id, UpdateInput input, ClaimsPrincipal user) =>
{
    var owner = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
    if (owner is null) return Results.Unauthorized();
    if (!Enum.TryParse<WorkStatus>(input.Status, true, out var status)) return Results.BadRequest(new { error = "Invalid status." });
    try
    {
        var item = await service.UpdateAsync(owner, id, input.Title, status, input.ExpectedVersion);
        return Results.Ok(new ItemDto(item.Id, item.Title, item.Status.ToString(), item.Version));
    }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(new { error = "Version conflict." }); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).RequireAuthorization();
app.MapDelete("/api/work-items/{id:guid}", async (WorkItemService service, Guid id, int? expectedVersion, ClaimsPrincipal user) =>
{
    var owner = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
    if (owner is null) return Results.Unauthorized();
    if (expectedVersion is null || expectedVersion <= 0) return Results.BadRequest(new { error = "Expected version is required." });
    try
    {
        await service.DeleteAsync(owner, id, expectedVersion.Value);
        return Results.NoContent();
    }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(new { error = "Version conflict." }); }
}).RequireAuthorization();
app.Run();
public partial class Program { }
internal static class Accounts
{
    private static readonly (string Email, string Password, string Role)[] Users =
    {
        ("alice@example.test", "Alice!234", "User"),
        ("bob@example.test", "Bob!234", "User"),
        ("admin@example.test", "Admin!234", "Admin")
    };
    public static (string Email, string Role)? Find(string email, string password)
    {
        var match = Users.FirstOrDefault(u => u.Email == email && u.Password == password);
        return match.Email is null ? null : (match.Email, match.Role);
    }
}
