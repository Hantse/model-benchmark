using Benchmark.Application;
using Benchmark.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IWorkItemRepository, InMemoryWorkItemRepository>();
builder.Services.AddScoped<WorkItemService>();
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/auth/login", () => Results.StatusCode(501));
app.MapMethods("/api/work-items", ["GET", "POST"], () => Results.StatusCode(501));
app.MapMethods("/api/work-items/{id:guid}", ["PUT", "DELETE"], () => Results.StatusCode(501));
app.Run();
public partial class Program { }
