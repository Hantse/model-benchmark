using Benchmark.Business;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Benchmark.Host.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public sealed class AuthController(IAuthCommands commands, IConfiguration configuration) : ControllerBase
{
    [HttpPost("login")]
    public Task<IActionResult> Login(LoginInput input, CancellationToken ct = default) => Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status501NotImplemented));
}
