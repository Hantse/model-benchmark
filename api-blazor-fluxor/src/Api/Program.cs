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
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => { options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), ValidateIssuer = true, ValidIssuer = issuer, ValidateAudience = true, ValidAudience = audience, ValidateLifetime = true, ClockSkew = TimeSpan.Zero, NameClaimType = "sub" }; });
builder.Services.AddAuthorization();
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/auth/login", (LoginInput input, IConfiguration config) =>
{
    if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.Password)) return Results.BadRequest();
    var accounts = new Dictionary<string, (string Password, string Role)> { ["alice@example.test"] = ("Alice!234", "User"), ["bob@example.test"] = ("Bob!234", "User"), ["admin@example.test"] = ("Admin!234", "Admin") };
    if (!accounts.TryGetValue(input.Email, out var account) || account.Password != input.Password) return Results.Unauthorized();
    var token = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], [new Claim("sub", input.Email), new Claim(ClaimTypes.Role, account.Role)], DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
    return Results.Ok(new LoginResult(new JwtSecurityTokenHandler().WriteToken(token)));
});
app.MapGet("/api/work-items", (WorkItemService service, string? status, ClaimsPrincipal user) =>
{
    if (!user.Identity!.IsAuthenticated) return Results.Unauthorized();
    WorkStatus? filter = null;
    if (status is not null)
    {
        if (!Enum.TryParse<WorkStatus>(status, true, out var parsed)) return Results.BadRequest();
        filter = parsed;
    }
    var items = service.ListAsync(user.Identity.Name!, filter).GetAwaiter().GetResult();
    return Results.Ok(items.Select(i => new ItemDto(i.Id, i.Title, i.Status.ToString(), i.Version)));
}).RequireAuthorization();
app.MapPost("/api/work-items", (WorkItemService service, CreateInput input, ClaimsPrincipal user) =>
{
    if (!user.Identity!.IsAuthenticated) return Results.Unauthorized();
    try { var item = service.AddAsync(user.Identity.Name!, input.Title, Enum.TryParse<WorkStatus>(input.Status, true, out var s) ? s : WorkStatus.Todo).GetAwaiter().GetResult(); return Results.Created($"/api/work-items/{item.Id}", new ItemDto(item.Id, item.Title, item.Status.ToString(), item.Version)); }
    catch (ArgumentException) { return Results.BadRequest(); }
}).RequireAuthorization();
app.MapPut("/api/work-items/{id:guid}", (WorkItemService service, Guid id, UpdateInput input, ClaimsPrincipal user) =>
{
    if (!user.Identity!.IsAuthenticated) return Results.Unauthorized();
    if (!Enum.TryParse<WorkStatus>(input.Status, true, out var s)) return Results.BadRequest();
    try { var item = service.UpdateAsync(user.Identity.Name!, id, input.Title, s, input.ExpectedVersion).GetAwaiter().GetResult(); return Results.Ok(new ItemDto(item.Id, item.Title, item.Status.ToString(), item.Version)); }
    catch (ArgumentException) { return Results.BadRequest(); }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(); }
}).RequireAuthorization();
app.MapDelete("/api/work-items/{id:guid}", (WorkItemService service, Guid id, int? expectedVersion, ClaimsPrincipal user) =>
{
    if (!user.Identity!.IsAuthenticated) return Results.Unauthorized();
    if (expectedVersion is null) return Results.BadRequest();
    try { service.DeleteAsync(user.Identity.Name!, id, expectedVersion.Value).GetAwaiter().GetResult(); return Results.NoContent(); }
    catch (ArgumentException) { return Results.BadRequest(); }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(); }
}).RequireAuthorization();
app.Run();
public partial class Program { }
