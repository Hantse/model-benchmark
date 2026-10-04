namespace TaskApi;

public sealed record LoginRequest(string Email, string Password);
public sealed record TokenResponse(string AccessToken);
public sealed record TaskInput(string Title, bool Completed = false);
public sealed record TaskDto(Guid Id, string Title, bool Completed);
// Replace with a thread-safe per-user in-memory store. No external database required.
public sealed class TaskStore { }
