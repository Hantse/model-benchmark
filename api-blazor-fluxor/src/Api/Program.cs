using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Benchmark.Api;
using Benchmark.Application;
using Benchmark.Domain;
using Benchmark.Infrastructure;
using Microsoft.IdentityModel.Tokens;
var builder = WebApplication.CreateBuilder(args);
var key = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
var issuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is required.");
var audience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is required.");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
builder.Services.AddSingleton<IWorkItemRepository, InMemoryWorkItemRepository>();
builder.Services.AddScoped<WorkItemService>();
builder.Services.AddAuthentication("Bearer").AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
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
        return Results.BadRequest(new { error = "Credentials required." });
    var account = Accounts.Find(input.Email);
    if (account is null || account.Password != input.Password)
        return Results.Unauthorized();
    var token = new JwtSecurityToken(issuer, audience,
        [new Claim(JwtRegisteredClaimNames.Sub, account.Email), new Claim(ClaimTypes.Role, account.Role)],
        notBefore: DateTime.UtcNow, expires: DateTime.UtcNow.AddMinutes(15),
        signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
    return Results.Ok(new LoginResult(new JwtSecurityTokenHandler().WriteToken(token)));
});
app.MapGet("/api/work-items", async (WorkItemService service, ClaimsPrincipal user, string? status) =>
{
    var subject = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    if (subject is null) return Results.Unauthorized();
    WorkStatus? filter = null;
    if (status is not null)
    {
        if (!Enum.TryParse<WorkStatus>(status, true, out var parsed)) return Results.BadRequest(new { error = "Invalid status filter." });
        filter = parsed;
    }
    var items = await service.ListAsync(subject, filter);
    return Results.Ok(items.Select(i => new ItemDto(i.Id, i.Title, i.Status.ToString(), i.Version)));
}).RequireAuthorization();
app.MapPost("/api/work-items", async (WorkItemService service, ClaimsPrincipal user, CreateInput input) =>
{
    var subject = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    if (subject is null) return Results.Unauthorized();
    if (!Enum.TryParse<WorkStatus>(input.Status, true, out var status)) return Results.BadRequest(new { error = "Invalid status." });
    try
    {
        var item = await service.AddAsync(subject, input.Title, status);
        return Results.Created($"/api/work-items/{item.Id}", new ItemDto(item.Id, item.Title, item.Status.ToString(), item.Version));
    }
    catch (ArgumentException) { return Results.BadRequest(new { error = "Invalid title." }); }
}).RequireAuthorization();
app.MapPut("/api/work-items/{id:guid}", async (WorkItemService service, ClaimsPrincipal user, Guid id, UpdateInput input) =>
{
    var subject = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    if (subject is null) return Results.Unauthorized();
    if (!Enum.TryParse<WorkStatus>(input.Status, true, out var status)) return Results.BadRequest(new { error = "Invalid status." });
    try
    {
        var item = await service.UpdateAsync(subject, id, input.Title, status, input.ExpectedVersion);
        return Results.Ok(new ItemDto(item.Id, item.Title, item.Status.ToString(), item.Version));
    }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(); }
    catch (ArgumentException) { return Results.BadRequest(new { error = "Invalid input." }); }
}).RequireAuthorization();
app.MapDelete("/api/work-items/{id:guid}", async (WorkItemService service, ClaimsPrincipal user, Guid id, int? expectedVersion) =>
{
    var subject = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    if (subject is null) return Results.Unauthorized();
    if (expectedVersion is null) return Results.BadRequest(new { error = "expectedVersion required." });
    try
    {
        await service.DeleteAsync(subject, id, expectedVersion.Value);
        return Results.NoContent();
    }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(); }
    catch (ArgumentException) { return Results.BadRequest(new { error = "Invalid expectedVersion." }); }
}).RequireAuthorization();
app.Run();
public partial class Program { }
file static class Accounts
{
    public static readonly Account[] All =
    [
        new("alice@example.test", "Alice!234", "User"),
        new("bob@example.test", "Bob!234", "User"),
        new("admin@example.test", "Admin!234", "Admin")
    ];
    public static Account? Find(string email) => All.FirstOrDefault(a => a.Email == email);
    public sealed record Account(string Email, string Password, string Role);
}
