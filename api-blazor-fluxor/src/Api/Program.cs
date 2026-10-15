using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Benchmark.Application;
using Benchmark.Domain;
using Benchmark.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
var builder = WebApplication.CreateBuilder(args);
var signingKey = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Missing Jwt:SigningKey configuration.");
var issuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Missing Jwt:Issuer configuration.");
var audience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Missing Jwt:Audience configuration.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IWorkItemRepository, InMemoryWorkItemRepository>();
builder.Services.AddScoped<WorkItemService>();
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/auth/login", async (LoginRequest request, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password)) return Results.BadRequest(new { error = "Email and password are required." });
    var account = Accounts.Find(request.Email, request.Password);
    if (account is null) return Results.Unauthorized();
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!));
    var token = new JwtSecurityToken(issuer, audience, new[] { new Claim(JwtRegisteredClaimNames.Sub, account.Value.Email), new Claim(ClaimTypes.Role, account.Value.Role) }, expires: DateTimeOffset.UtcNow.AddMinutes(15), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature));
    return Results.Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token) });
}).AllowAnonymous();
app.MapGet("/api/work-items", async (WorkItemService service, string? status, HttpContext context) =>
{
    if (!Enum.TryParse<WorkStatus>(status, true, out var parsed)) return Results.BadRequest(new { error = "Invalid status filter." });
    return Results.Ok(await service.ListAsync(context.User.Identity!.Name!, parsed));
}).RequireAuthorization();
app.MapPost("/api/work-items", async (WorkItemService service, CreateWorkItemRequest request, HttpContext context) =>
{
    if (!Enum.TryParse<WorkStatus>(request.Status, true, out var status)) return Results.BadRequest(new { error = "Invalid status." });
    var item = await service.AddAsync(context.User.Identity!.Name!, request.Title!, status);
    return Results.Created($"/api/work-items/{item.Id}", item);
}).RequireAuthorization();
app.MapPut("/api/work-items/{id:guid}", async (WorkItemService service, Guid id, UpdateWorkItemRequest request, HttpContext context) =>
{
    if (!Enum.TryParse<WorkStatus>(request.Status, true, out var status)) return Results.BadRequest(new { error = "Invalid status." });
    if (request.ExpectedVersion < 1) return Results.BadRequest(new { error = "Expected version must be positive." });
    try
    {
        return Results.Ok(await service.UpdateAsync(context.User.Identity!.Name!, id, request.Title!, status, request.ExpectedVersion));
    }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(new { error = "Version conflict." }); }
}).RequireAuthorization();
app.MapDelete("/api/work-items/{id:guid}", async (WorkItemService service, Guid id, int? expectedVersion, HttpContext context) =>
{
    if (expectedVersion is null or < 1) return Results.BadRequest(new { error = "Expected version must be positive." });
    try
    {
        await service.DeleteAsync(context.User.Identity!.Name!, id, expectedVersion.Value);
        return Results.NoContent();
    }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(new { error = "Version conflict." }); }
}).RequireAuthorization();
app.Run();
public record LoginRequest(string? Email, string? Password);
public record CreateWorkItemRequest(string? Title, string? Status);
public record UpdateWorkItemRequest(string? Title, string? Status, int ExpectedVersion);
public static class Accounts
{
    private static readonly (string Email, string Password, string Role)[] Users =
    [
        ("alice@example.test", "Alice!234", "User"),
        ("bob@example.test", "Bob!234", "User"),
        ("admin@example.test", "Admin!234", "Admin")
    ];
    public static (string Email, string Role)? Find(string email, string password) => Users.FirstOrDefault(x => x.Email == email && x.Password == password) is var found && found is not null ? (found.Email, found.Role) : null;
}
