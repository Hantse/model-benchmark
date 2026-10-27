using Benchmark.Domain;
using Benchmark.Infrastructure;
using OneOf;
namespace Benchmark.Business;

public sealed record LoginCommand(string Email, string Password);
public sealed record Unauthorized;
public interface IAuthCommands
{
    Task<OneOf<AuthenticatedAccount, ValidationError, Unauthorized>> Handle(LoginCommand command, CancellationToken ct = default);
}

// Orchestrate authentication and translate expected outcomes into a typed union.
public sealed class AuthCommands(IAuthenticationService service) : IAuthCommands
{
    public Task<OneOf<AuthenticatedAccount, ValidationError, Unauthorized>> Handle(LoginCommand command, CancellationToken ct = default) => throw new NotImplementedException();
}
