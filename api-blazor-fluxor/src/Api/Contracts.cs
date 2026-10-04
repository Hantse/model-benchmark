namespace Benchmark.Api;
public sealed record LoginInput(string Email, string Password);
public sealed record LoginResult(string AccessToken);
public sealed record CreateInput(string Title, string Status = "Todo");
public sealed record UpdateInput(string Title, string Status, int ExpectedVersion);
public sealed record ItemDto(Guid Id, string Title, string Status, int Version);
