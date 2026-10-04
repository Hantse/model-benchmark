using TaskApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<TaskStore>();
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
// Features intentionally unimplemented. Replace these routes in src/ only.
app.MapPost("/api/auth/login", () => Results.StatusCode(501));
app.MapMethods("/api/tasks", ["GET", "POST"], () => Results.StatusCode(501));
app.MapMethods("/api/tasks/{id:guid}", ["PUT", "DELETE"], () => Results.StatusCode(501));

app.Run();
public partial class Program { }
