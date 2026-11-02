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
app.MapPost("/api/auth/login", (LoginInput input, IConfiguration config) =>
{
    if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.Password)) return Results.BadRequest();
    string? email = null;
    string? role = null;
    if (input.Email == "alice@example.test" && input.Password == "Alice!234") { email = "alice@example.test"; role = "User"; }
    else if (input.Email == "bob@example.test" && input.Password == "Bob!234") { email = "bob@example.test"; role = "User"; }
    else if (input.Email == "admin@example.test" && input.Password == "Admin!234") { email = "admin@example.test"; role = "Admin"; }
    if (email is null) return Results.Unauthorized();
    var now = DateTime.UtcNow;
    var token = new JwtSecurityToken(issuer, audience, new List<Claim> { new Claim(JwtRegisteredClaimNames.Sub, email), new Claim(ClaimTypes.Role, role) }, now.AddMinutes(-1), now.AddMinutes(15), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
    return Results.Ok(new LoginResult(new JwtSecurityTokenHandler().WriteToken(token)));
});
app.MapGet("/api/work-items", async (WorkItemService service, string? status, ClaimsPrincipal user) =>
{
    if (user.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    WorkStatus? parsed = null;
    if (status is not null && !Enum.TryParse<WorkStatus>(status, out parsed)) return Results.BadRequest();
    var items = await service.ListAsync(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? string.Empty, parsed);
    return Results.Ok(items.Select(i => new ItemDto(i.Id, i.Title, i.Status.ToString(), i.Version)));
}).RequireAuthorization();
app.MapPost("/api/work-items", async (WorkItemService service, CreateInput input, ClaimsPrincipal user) =>
{
    if (user.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    if (!Enum.TryParse<WorkStatus>(input.Status, out var status)) return Results.BadRequest();
    try { var item = await service.AddAsync(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? string.Empty, input.Title, status); return Results.Created($"/api/work-items/{item.Id}", new ItemDto(item.Id, item.Title, item.Status.ToString(), item.Version)); }
    catch (ArgumentException) { return Results.BadRequest(); }
}).RequireAuthorization();
app.MapPut("/api/work-items/{id:guid}", async (WorkItemService service, Guid id, UpdateInput input, ClaimsPrincipal user) =>
{
    if (user.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    if (!Enum.TryParse<WorkStatus>(input.Status, out var status)) return Results.BadRequest();
    try { var item = await service.UpdateAsync(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? string.Empty, id, input.Title, status, input.ExpectedVersion); return Results.Ok(new ItemDto(item.Id, item.Title, item.Status.ToString(), item.Version)); }
    catch (ArgumentException) { return Results.BadRequest(); }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(); }
}).RequireAuthorization();
app.MapDelete("/api/work-items/{id:guid}", async (WorkItemService service, Guid id, int? expectedVersion, ClaimsPrincipal user) =>
{
    if (user.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    if (expectedVersion is null || expectedVersion < 1) return Results.BadRequest();
    try { await service.DeleteAsync(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? string.Empty, id, expectedVersion.Value); return Results.NoContent(); }
    catch (ArgumentException) { return Results.BadRequest(); }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(); }
}).RequireAuthorization();
app.Run();
public partial class Program { }
