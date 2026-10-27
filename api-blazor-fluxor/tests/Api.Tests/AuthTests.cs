using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Benchmark.Business;
using Benchmark.Domain;
using Benchmark.Host;
using Benchmark.Host.Controllers;
using Benchmark.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OneOf;
using Xunit;
namespace Benchmark.Tests;

public sealed class AuthTests
{
    [Theory]
    [InlineData("alice@example.test", "Alice!234", "User")]
    [InlineData("bob@example.test", "Bob!234", "User")]
    [InlineData("admin@example.test", "Admin!234", "Admin")]
    public async Task Synthetic_accounts_authenticate_in_infrastructure(string email, string password, string role)
    {
        Assert.Equal(new AuthenticatedAccount(email, role), await new AuthenticationService().AuthenticateAsync(email, password));
    }
    [Theory]
    [InlineData("alice@example.test", "wrong")][InlineData("unknown@example.test", "Alice!234")]
    public async Task Invalid_credentials_do_not_authenticate(string email, string password)
    {
        Assert.Null(await new AuthenticationService().AuthenticateAsync(email, password));
    }
    [Theory]
    [InlineData("", "Alice!234")][InlineData("alice@example.test", "")][InlineData(" ", "Alice!234")][InlineData("alice@example.test", " ")]
    public async Task Missing_credentials_are_validation_errors(string email, string password)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => new AuthenticationService().AuthenticateAsync(email, password));
    }
    [Fact]
    public async Task Authentication_observes_cancellation()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AuthenticationService().AuthenticateAsync("alice@example.test", "Alice!234", cancellation.Token));
    }
    [Fact]
    public async Task Auth_CQRS_forwards_credentials_and_cancellation_and_returns_account()
    {
        var service = new ControlledAuthentication(); using var cancellation = new CancellationTokenSource();
        var result = await new AuthCommands(service).Handle(new("synthetic@example.test", "test-only"), cancellation.Token);
        Assert.True(result.IsT0); Assert.Equal(service.Account, result.AsT0);
        Assert.Equal(("synthetic@example.test", "test-only", cancellation.Token), (service.Email, service.Password, service.Token)); Assert.Equal(1, service.Calls);
    }
    [Fact]
    public async Task Auth_CQRS_maps_no_account_to_typed_unauthorized()
    {
        var service = new ControlledAuthentication { Account = null };
        Assert.True((await new AuthCommands(service).Handle(new("alice@example.test", "wrong"))).IsT2); Assert.Equal(1, service.Calls);
    }
    [Fact]
    public async Task Auth_CQRS_maps_validation_and_preserves_diagnostic()
    {
        var service = new ControlledAuthentication { Failure = new ArgumentException("Email is required") };
        var result = await new AuthCommands(service).Handle(new("", "test-only"));
        Assert.True(result.IsT1); Assert.Equal("Email is required", result.AsT1.Message); Assert.Equal(1, service.Calls);
    }
    [Fact]
    public async Task Auth_CQRS_does_not_hide_or_retry_unexpected_failure()
    {
        var failure = new InvalidOperationException("Account store unavailable"); var service = new ControlledAuthentication { Failure = failure };
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => new AuthCommands(service).Handle(new("alice@example.test", "test-only")))); Assert.Equal(1, service.Calls);
    }
    [Fact]
    public async Task Auth_CQRS_does_not_hide_or_retry_cancellation()
    {
        var failure = new OperationCanceledException(); var service = new ControlledAuthentication { Failure = failure };
        Assert.Same(failure, await Assert.ThrowsAsync<OperationCanceledException>(() => new AuthCommands(service).Handle(new("alice@example.test", "test-only")))); Assert.Equal(1, service.Calls);
    }
    [Fact]
    public async Task Auth_controller_signs_the_CQRS_identity_using_configured_JWT_material()
    {
        var commands = new ControlledAuthCommands(); using var cancellation = new CancellationTokenSource();
        var signingKey = "synthetic-controller-test-key-" + Guid.NewGuid().ToString("N");
        var issuer = "issuer-" + Guid.NewGuid().ToString("N"); var audience = "audience-" + Guid.NewGuid().ToString("N");
        var controller = new AuthController(commands, Configuration(signingKey, issuer, audience));
        var result = Assert.IsType<OkObjectResult>(await controller.Login(new("form@example.test", "test-only"), cancellation.Token));
        var token = Assert.IsType<LoginResult>(result.Value).AccessToken;
        var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateIssuer = true, ValidIssuer = issuer, ValidateAudience = true, ValidAudience = audience,
            ValidateLifetime = true, ClockSkew = TimeSpan.Zero
        }, out var securityToken);
        Assert.InRange(securityToken.ValidTo - DateTime.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(16));
        Assert.Equal("returned@example.test", principal.FindFirst("sub")?.Value); Assert.Equal("Admin", principal.FindFirst(ClaimTypes.Role)?.Value);
        Assert.Equal(new LoginCommand("form@example.test", "test-only"), commands.Command); Assert.Equal(cancellation.Token, commands.Token); Assert.Equal(1, commands.Calls);
    }
    [Fact]
    public async Task Auth_controller_maps_CQRS_validation_to_400()
    {
        var commands = new ControlledAuthCommands { Result = new ValidationError("Missing password") };
        var result = Assert.IsType<BadRequestObjectResult>(await new AuthController(commands, Configuration()).Login(new("x", "")));
        Assert.Contains("Missing password", System.Text.Json.JsonSerializer.Serialize(result.Value)); Assert.Equal(1, commands.Calls);
    }
    [Fact]
    public async Task Auth_controller_maps_CQRS_unauthorized_to_401()
    {
        var commands = new ControlledAuthCommands { Result = new Unauthorized() };
        Assert.IsType<UnauthorizedResult>(await new AuthController(commands, Configuration()).Login(new("x", "wrong"))); Assert.Equal(1, commands.Calls);
    }
    private static IConfiguration Configuration(string signingKey = ApiFixture.Key, string issuer = ApiFixture.Issuer, string audience = ApiFixture.Audience) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = signingKey, ["Jwt:Issuer"] = issuer, ["Jwt:Audience"] = audience }).Build();
    private sealed class ControlledAuthentication : IAuthenticationService
    {
        public AuthenticatedAccount? Account = new("returned@example.test", "Admin"); public Exception? Failure; public string? Email, Password; public int Calls; public CancellationToken Token;
        public Task<AuthenticatedAccount?> AuthenticateAsync(string email, string password, CancellationToken ct = default)
        {
            Email = email; Password = password; Token = ct; Calls++; if (Failure is not null) throw Failure; return Task.FromResult(Account);
        }
    }
    private sealed class ControlledAuthCommands : IAuthCommands
    {
        public OneOf<AuthenticatedAccount, ValidationError, Unauthorized> Result = new AuthenticatedAccount("returned@example.test", "Admin");
        public LoginCommand? Command; public CancellationToken Token; public int Calls;
        public Task<OneOf<AuthenticatedAccount, ValidationError, Unauthorized>> Handle(LoginCommand command, CancellationToken ct = default) { Command = command; Token = ct; Calls++; return Task.FromResult(Result); }
    }
}
