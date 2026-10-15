using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Benchmark.Application;
using Benchmark.Domain;
using Benchmark.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
var builder = WebApplication.CreateBuilder(args);
var signingKey = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
var issuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is required.");
var audience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is required.");
var accounts = new Dictionary<string, (string Password, string Role)>
{
    ["alice@example.test"] = ("Alice!234", "User"),
    ["bob@example.test"] = ("Bob!234", "User"),
    ["admin@example.test"] = ("Admin!234", "Admin")
};
builder.Services.AddSingleton(new InMemoryWorkItemRepository());
builder.Services.AddScoped<WorkItemService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
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
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/auth/login", async (HttpRequest request) =>
{
    var credentials = await request.ReadFromJsonAsync<LoginRequest>();
    if (string.IsNullOrWhiteSpace(credentials?.Email) || string.IsNullOrWhiteSpace(credentials?.Password)) return Results.BadRequest();
    if (!accounts.TryGetValue(credentials.Email.Trim(), out var account) || account.Password != credentials.Password) return Results.Unauthorized();
    var token = new JwtSecurityToken(issuer, audience, new[] { new Claim(JwtRegisteredClaimNames.Sub, credentials.Email.Trim()), new Claim(ClaimTypes.Role, account.Role) }, expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256));
    return Results.Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token) });
});
app.MapGet("/api/work-items", async (WorkItemService service, HttpRequest request, string? status) =>
{
    if (!request.HttpContext.User.Identity?.IsAuthenticated == true) return Results.Unauthorized();
    WorkStatus? parsedStatus = null;
    if (status is not null)
    {
        if (!Enum.TryParse<WorkStatus>(status, out var parsed)) return Results.BadRequest();
        parsedStatus = parsed;
    }
    return Results.Ok(await service.ListAsync(request.HttpContext.User.Identity!.Name!, parsedStatus));
}).RequireAuthorization();
app.MapPost("/api/work-items", async (WorkItemService service, HttpRequest request, CreateWorkItemRequest? body) =>
{
    if (!request.HttpContext.User.Identity?.IsAuthenticated == true) return Results.Unauthorized();
    if (body is null || !Enum.TryParse<WorkStatus>(body.Status ?? "Todo", out var status)) return Results.BadRequest();
    var item = await service.AddAsync(request.HttpContext.User.Identity!.Name!, body.Title!, status);
    return Results.Created($"/api/work-items/{item.Id}", item);
}).RequireAuthorization();
app.MapPut("/api/work-items/{id:guid}", async (WorkItemService service, HttpRequest request, Guid id, UpdateWorkItemRequest? body) =>
{
    if (!request.HttpContext.User.Identity?.IsAuthenticated == true) return Results.Unauthorized();
    if (body is null || !Enum.TryParse<WorkStatus>(body.Status, out var status)) return Results.BadRequest();
    try
    {
        return Results.Ok(await service.UpdateAsync(request.HttpContext.User.Identity!.Name!, id, body.Title!, status, body.ExpectedVersion));
    }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(); }
    catch (ArgumentException) { return Results.BadRequest(); }
}).RequireAuthorization();
app.MapDelete("/api/work-items/{id:guid}", async (WorkItemService service, HttpRequest request, Guid id, int? expectedVersion) =>
{
    if (!request.HttpContext.User.Identity?.IsAuthenticated == true) return Results.Unauthorized();
    if (expectedVersion is null) return Results.BadRequest();
    try
    {
        await service.DeleteAsync(request.HttpContext.User.Identity!.Name!, id, expectedVersion.Value);
        return Results.NoContent();
    }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(); }
    catch (ArgumentException) { return Results.BadRequest(); }
}).RequireAuthorization();
app.Run();
public sealed record LoginRequest(string? Email, string? Password);
public sealed record CreateWorkItemRequest(string? Title, string? Status);
public sealed record UpdateWorkItemRequest(string? Title, string? Status, int ExpectedVersion);
