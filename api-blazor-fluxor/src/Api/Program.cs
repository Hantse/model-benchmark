using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Benchmark.Application;
using Benchmark.Domain;
using Benchmark.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
var issuer = jwt["Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is required.");
var audience = jwt["Audience"] ?? throw new InvalidOperationException("Jwt:Audience is required.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
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
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IWorkItemRepository, InMemoryWorkItemRepository>();
builder.Services.AddScoped<WorkItemService>();
builder.Services.AddControllers();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();

public partial class Program;
