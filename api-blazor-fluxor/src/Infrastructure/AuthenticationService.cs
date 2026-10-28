using Benchmark.Domain;
namespace Benchmark.Infrastructure;

public interface IAuthenticationService
{
    Task<AuthenticatedAccount?> AuthenticateAsync(string email, string password, CancellationToken ct = default);
}

public sealed class AuthenticationService : IAuthenticationService
{
    private static readonly (string Email, string Password, string Role)[] Accounts =
    [
        ("alice@example.test", "Alice!234", "User"),
        ("bob@example.test", "Bob!234", "User"),
        ("admin@example.test", "Admin!234", "Admin")
    ];

    public Task<AuthenticatedAccount?> AuthenticateAsync(string email, string password, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Email and password are required.");
        var match = Accounts.FirstOrDefault(a => a.Email == email && a.Password == password);
        return Task.FromResult<AuthenticatedAccount?>(match.Email is null ? null : new AuthenticatedAccount(match.Email, match.Role));
    }
}
