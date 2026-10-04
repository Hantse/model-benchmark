namespace WorkBoard.Api;

public sealed record LoginRequest(string? Email, string? Password);
public sealed record LoginResponse(string AccessToken);
public sealed record CreateWorkItemRequest(string? Title, string? Status = "Todo");
public sealed record UpdateWorkItemRequest(string? Title, string? Status, int ExpectedVersion);
public sealed record WorkItemDto(Guid Id, string Title, string Status, int Version);
