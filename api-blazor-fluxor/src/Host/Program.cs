using Benchmark.Business;
using Benchmark.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddAuthorization();
builder.Services.AddScoped<IWorkItemRepository>(services => new SqliteWorkItemRepository(services.GetRequiredService<IConfiguration>().GetConnectionString("WorkItems") ?? "Data Source=work-items.db"));
builder.Services.AddScoped<IWorkItemService, WorkItemService>();
builder.Services.AddScoped<IWorkItemQueries, WorkItemQueries>();
builder.Services.AddScoped<IWorkItemCommands, WorkItemCommands>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IAuthCommands, AuthCommands>();
var app = builder.Build();
// Configure signed JWT validation and initialize SQLite during startup as part of the task.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
public partial class Program { }
