using Benchmark.Business;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Benchmark.Host.Controllers;

[ApiController]
[Authorize]
[Route("api/work-items")]
public sealed class WorkItemsController(IWorkItemQueries queries, IWorkItemCommands commands) : ControllerBase
{
    // Consume the Business OneOf results here and map every expected outcome to HTTP.
    [HttpGet]
    public Task<IActionResult> List([FromQuery] string? status = null, CancellationToken ct = default) => Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status501NotImplemented));
    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateInput input, CancellationToken ct = default) => Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status501NotImplemented));
    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateInput input, CancellationToken ct = default) => Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status501NotImplemented));
    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, [FromQuery] int? expectedVersion = null, CancellationToken ct = default) => Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status501NotImplemented));
}
