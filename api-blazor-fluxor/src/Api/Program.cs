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
var key = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
var issuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is required.");
var audience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is required.");
builder.Services.AddSingleton<IWorkItemRepository, InMemoryWorkItemRepository>();
builder.Services.AddScoped<WorkItemService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
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
app.MapPost("/api/auth/login", (LoginInput input) =>
{
    if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.Password)) return Results.BadRequest();
    var account = Accounts.Find(input.Email);
    if (account is null || account.Password != input.Password) return Results.Unauthorized();
    var token = new JwtSecurityToken(issuer, audience, [new Claim(JwtRegisteredClaimNames.Sub, account.Email), new Claim(ClaimTypes.Role, account.Role)], DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
    return Results.Ok(new LoginResult(new JwtSecurityTokenHandler().WriteToken(token)));
}).AllowAnonymous();
app.MapGet("/api/work-items", async (WorkItemService service, HttpContext context, string? status) =>
{
    var owner = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? throw new InvalidOperationException();
    WorkStatus? filter = null;
    if (status is not null)
    {
        if (!Enum.TryParse<WorkStatus>(status, true, out var parsed)) return Results.BadRequest();
        filter = parsed;
    }
    return Results.Ok(await service.ListAsync(owner, filter));
}).RequireAuthorization();
app.MapPost("/api/work-items", async (WorkItemService service, HttpContext context, CreateInput input) =>
{
    var owner = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? throw new InvalidOperationException();
    WorkStatus status;
    try { status = input.Status is null ? WorkStatus.Todo : Enum.Parse<WorkStatus>(input.Status, true); }
    catch { return Results.BadRequest(); }
    try { var item = await service.AddAsync(owner, input.Title, status); return Results.Created($"/api/work-items/{item.Id}", item); }
    catch (ArgumentException) { return Results.BadRequest(); }
}).RequireAuthorization();
app.MapPut("/api/work-items/{id:guid}", async (WorkItemService service, HttpContext context, Guid id, UpdateInput input) =>
{
    var owner = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? throw new InvalidOperationException();
    WorkStatus status;
    try { status = Enum.Parse<WorkStatus>(input.Status, true); }
    catch { return Results.BadRequest(); }
    try { var item = await service.UpdateAsync(owner, id, input.Title, status, input.ExpectedVersion); return Results.Ok(item); }
    catch (ArgumentException) { return Results.BadRequest(); }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(); }
}).RequireAuthorization();
app.MapDelete("/api/work-items/{id:guid}", async (WorkItemService service, HttpContext context, Guid id, int? expectedVersion) =>
{
    var owner = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? throw new InvalidOperationException();
    if (expectedVersion is null) return Results.BadRequest();
    try { await service.DeleteAsync(owner, id, expectedVersion.Value); return Results.NoContent(); }
    catch (ArgumentException) { return Results.BadRequest(); }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(); }
}).RequireAuthorization();
app.Run();
public static class Accounts
{
    public static readonly Account[] All = [new("alice@example.test", "Alice!234", "User"), new("bob@example.test", "Bob!234", "User"), new("admin@example.test", "Admin!234", "Admin")];
    public static Account? Find(string email) => All.FirstOrDefault(a => a.Email == email);
}
public sealed record Account(string Email, string Password, string Role);
