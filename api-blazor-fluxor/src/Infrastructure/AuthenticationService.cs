using Benchmark.Domain;
namespace Benchmark.Infrastructure;

public interface IAuthenticationService
{
    Task<AuthenticatedAccount?> AuthenticateAsync(string email, string password, CancellationToken ct = default);
}

// Implement the documented synthetic account rules. No JWT or HTTP types belong here.
public sealed class AuthenticationService : IAuthenticationService
{
    public Task<AuthenticatedAccount?> AuthenticateAsync(string email, string password, CancellationToken ct = default) => throw new NotImplementedException();
}
