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
builder.Services.AddSingleton<IWorkItemRepository, InMemoryWorkItemRepository>();
builder.Services.AddScoped<WorkItemService>();
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/auth/login", (LoginInput input, IConfiguration config) =>
{
    if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.Password)) return Results.BadRequest();
    var accounts = new Dictionary<string, (string Password, string Role)>
    {
        ["alice@example.test"] = ("Alice!234", "User"),
        ["bob@example.test"] = ("Bob!234", "User"),
        ["admin@example.test"] = ("Admin!234", "Admin")
    };
    if (!accounts.TryGetValue(input.Email, out var account) || account.Password != input.Password) return Results.Unauthorized();
    var signingKey = config["Jwt:SigningKey"]!;
    var token = new JwtSecurityToken(
        issuer: config["Jwt:Issuer"]!,
        audience: config["Jwt:Audience"]!,
        claims: [new Claim(JwtRegisteredClaimNames.Sub, input.Email), new Claim(ClaimTypes.Role, account.Role)],
        notBefore: DateTime.UtcNow,
        expires: DateTime.UtcNow.AddMinutes(15),
        signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256));
    return Results.Ok(new LoginResult(new JwtSecurityTokenHandler().WriteToken(token)));
});
app.MapGet("/api/work-items", (WorkItemService service, ClaimsPrincipal user, string? status) =>
{
    var owner = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (owner is null) return Results.Unauthorized();
    WorkStatus? filter = null;
    if (status is not null)
    {
        if (!Enum.TryParse<WorkStatus>(status, true, out var parsed)) return Results.BadRequest();
        filter = parsed;
    }
    var items = service.ListAsync(owner, filter).GetAwaiter().GetResult();
    return Results.Ok(items.Select(i => new ItemDto(i.Id, i.Title, i.Status.ToString(), i.Version)));
}).RequireAuthorization();
app.MapPost("/api/work-items", (WorkItemService service, ClaimsPrincipal user, CreateInput input) =>
{
    var owner = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (owner is null) return Results.Unauthorized();
    WorkStatus status;
    if (string.IsNullOrWhiteSpace(input.Status)) status = WorkStatus.Todo;
    else if (!Enum.TryParse<WorkStatus>(input.Status, true, out var parsed)) return Results.BadRequest();
    else status = parsed;
    try
    {
        var item = service.AddAsync(owner, input.Title ?? string.Empty, status).GetAwaiter().GetResult();
        return Results.Created($"/api/work-items/{item.Id}", new ItemDto(item.Id, item.Title, item.Status.ToString(), item.Version));
    }
    catch (ArgumentException) { return Results.BadRequest(); }
}).RequireAuthorization();
app.MapPut("/api/work-items/{id:guid}", (WorkItemService service, ClaimsPrincipal user, Guid id, UpdateInput input) =>
{
    var owner = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (owner is null) return Results.Unauthorized();
    if (!Enum.TryParse<WorkStatus>(input.Status, true, out var status)) return Results.BadRequest();
    try
    {
        var item = service.UpdateAsync(owner, id, input.Title ?? string.Empty, status, input.ExpectedVersion).GetAwaiter().GetResult();
        return Results.Ok(new ItemDto(item.Id, item.Title, item.Status.ToString(), item.Version));
    }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(); }
    catch (ArgumentException) { return Results.BadRequest(); }
}).RequireAuthorization();
app.MapDelete("/api/work-items/{id:guid}", (WorkItemService service, ClaimsPrincipal user, Guid id, int? expectedVersion) =>
{
    var owner = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (owner is null) return Results.Unauthorized();
    if (expectedVersion is null || expectedVersion < 1) return Results.BadRequest();
    try
    {
        service.DeleteAsync(owner, id, expectedVersion.Value).GetAwaiter().GetResult();
        return Results.NoContent();
    }
    catch (ItemNotFoundException) { return Results.NotFound(); }
    catch (VersionConflictException) { return Results.Conflict(); }
}).RequireAuthorization();
app.Run();
