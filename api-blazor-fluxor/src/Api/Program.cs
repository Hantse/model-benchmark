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
builder.Services.AddSingleton<IWorkItemRepository, InMemoryWorkItemRepository>();
builder.Services.AddScoped<WorkItemService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        NameClaimType = JwtRegisteredClaimNames.Sub,
        RoleClaimType = ClaimTypes.Role
    };
});
builder.Services.AddAuthorization();
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/auth/login", (LoginInput input, IConfiguration cfg) =>
{
    if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.Password)) return Results.BadRequest();
    var account = Accounts.FirstOrDefault(a => a.Email == input.Email);
    if (account is null || account.Password != input.Password) return Results.Unauthorized();
    var token = new JwtSecurityToken(issuer, audience, [new Claim(JwtRegisteredClaimNames.Sub, input.Email), new Claim(ClaimTypes.Role, account.Role)], DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
    return Results.Ok(new LoginResult(new JwtSecurityTokenHandler().WriteToken(token)));
});
app.MapGet("/api/work-items", async (WorkItemService svc, string? status, ClaimsPrincipal user) =>
{
    if (user.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    if (status is not null && !Enum.TryParse<WorkStatus>(status, out var s)) return Results.BadRequest();
    var items = await svc.ListAsync(user.FindFirst(JwtRegisteredClaimNames.Sub)!.Value, status is null ? null : s);
    return Results.Ok(items.Select(i => new ItemDto(i.Id, i.Title, i.Status.ToString(), i.Version)));
}).RequireAuthorization();
app.MapPost("/api/work-items", async (WorkItemService svc, CreateInput input, ClaimsPrincipal user) =>
{
    if (user.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    if (!Enum.TryParse<WorkStatus>(input.Status, out var s)) return Results.BadRequest();
    try { var item = await svc.AddAsync(user.FindFirst(JwtRegisteredClaimNames.Sub)!.Value, input.Title, s); return Results.Created($"/api/work-items/{item.Id}", new ItemDto(item.Id, item.Title, item.Status.ToString(), item.Version)); }
    catch (ArgumentException) { return Results.BadRequest(); }
}).RequireAuthorization();
app.MapPut("/api/work-items/{id:guid}", async (WorkItemService svc, Guid id, UpdateInput input, ClaimsPrincipal user) =>
{
    if (user.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    if (!Enum.TryParse<WorkStatus>(input.Status, out var s)) return Results.BadRequest();
    try { var item = await svc.UpdateAsync(user.FindFirst(JwtRegisteredClaimNames.Sub)!.Value, id, input.Title, s, input.ExpectedVersion); return Results.Ok(new ItemDto(item.Id, item.Title, item.Status.ToString(), item.Version)); }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(); }
    catch (ArgumentException) { return Results.BadRequest(); }
}).RequireAuthorization();
app.MapDelete("/api/work-items/{id:guid}", async (WorkItemService svc, Guid id, int? expectedVersion, ClaimsPrincipal user) =>
{
    if (user.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    if (expectedVersion is null || expectedVersion <= 0) return Results.BadRequest();
    try { await svc.DeleteAsync(user.FindFirst(JwtRegisteredClaimNames.Sub)!.Value, id, expectedVersion.Value); return Results.NoContent(); }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(); }
    catch (ArgumentException) { return Results.BadRequest(); }
}).RequireAuthorization();
app.Run();
public partial class Program { }
static class Accounts { public static readonly (string Email, string Password, string Role)[] Data = [("alice@example.test","Alice!234","User"),("bob@example.test","Bob!234","User"),("admin@example.test","Admin!234","Admin")]; public static (string Email, string Password, string Role)? FirstOrDefault(string email) => Data.FirstOrDefault(a => a.Email == email); }
