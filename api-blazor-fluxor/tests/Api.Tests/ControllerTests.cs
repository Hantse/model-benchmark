using System.Security.Claims;
using System.Text.Json;
using System.Reflection;
using System.Reflection.Emit;
using Benchmark.Business;
using Benchmark.Domain;
using Benchmark.Host;
using Benchmark.Host.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OneOf;
using Xunit;
namespace Benchmark.Tests;

public sealed class ControllerTests
{
    [Fact]
    public async Task Every_application_route_is_an_MVC_controller_action()
    {
        using var host = new ApiFixture(); using var client = host.CreateClient(); await client.GetAsync("/health");
        var endpoints = host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToArray();
        foreach (var route in new[] { ("health", "GET"), ("api/auth/login", "POST"), ("api/work-items", "GET"), ("api/work-items", "POST"), ("api/work-items/{id:guid}", "PUT"), ("api/work-items/{id:guid}", "DELETE") })
        {
            var endpoint = Assert.Single(endpoints, x => x.RoutePattern.RawText?.Trim('/') == route.Item1 && x.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(route.Item2));
            Assert.NotNull(endpoint.Metadata.GetMetadata<ControllerActionDescriptor>());
        }
    }
    [Fact]
    public void Compiled_controller_consumes_OneOf_with_Match_or_Switch()
    {
        var methods = ControllerTypes(typeof(WorkItemsController)).SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Contains(methods.SelectMany(CalledMethods), method => method.DeclaringType?.Namespace == "OneOf" && method.Name is "Match" or "Switch");
    }
    [Fact]
    public async Task List_consumes_query_success_and_maps_entities_without_owner()
    {
        var business = new ControlledBusiness(); var controller = Create(business); using var cancellation = new CancellationTokenSource();
        var result = Assert.IsType<OkObjectResult>(await controller.List("Done", cancellation.Token));
        Assert.Equal(new[] { new ItemDto(business.Item.Id, business.Item.Title, "Done", business.Item.Version) }, Assert.IsAssignableFrom<IEnumerable<ItemDto>>(result.Value));
        Assert.Equal(new ListWorkItems("alice@example.test", WorkStatus.Done), Assert.Single(business.Queries)); Assert.Equal(cancellation.Token, business.Token); Assert.Empty(business.Commands);
    }
    [Fact]
    public async Task List_consumes_query_validation_as_400_with_diagnostic()
    {
        var business = new ControlledBusiness { ListResult = new ValidationError("Invalid owner") };
        var result = Assert.IsType<BadRequestObjectResult>(await Create(business).List());
        Assert.Contains("Invalid owner", JsonSerializer.Serialize(result.Value)); Assert.Single(business.Queries);
    }
    [Fact]
    public async Task Invalid_filter_is_rejected_before_dispatching_a_query()
    {
        var business = new ControlledBusiness(); Assert.IsType<BadRequestObjectResult>(await Create(business).List("done")); Assert.Empty(business.Queries); Assert.Empty(business.Commands);
    }
    [Fact]
    public async Task Create_consumes_success_as_201_with_relative_location_and_authenticated_owner()
    {
        var business = new ControlledBusiness(); using var cancellation = new CancellationTokenSource();
        var result = Assert.IsType<CreatedResult>(await Create(business).Create(new("  raw  ", "InProgress"), cancellation.Token));
        Assert.Equal($"/api/work-items/{business.Item.Id}", result.Location); Assert.Equal(Dto(business.Item), result.Value);
        Assert.Equal(new CreateWorkItem("alice@example.test", "  raw  ", WorkStatus.InProgress), Assert.Single(business.Commands)); Assert.Equal(cancellation.Token, business.Token); Assert.Empty(business.Queries);
    }
    [Fact]
    public async Task Create_consumes_typed_validation_as_400()
    {
        var business = new ControlledBusiness { CreateResult = new ValidationError("Title is too long") };
        var result = Assert.IsType<BadRequestObjectResult>(await Create(business).Create(new("x")));
        Assert.Contains("Title is too long", JsonSerializer.Serialize(result.Value)); Assert.Single(business.Commands);
    }
    [Fact]
    public async Task Create_rejects_unknown_status_before_dispatch()
    {
        var business = new ControlledBusiness(); Assert.IsType<BadRequestObjectResult>(await Create(business).Create(new("x", "42"))); Assert.Empty(business.Commands);
    }
    [Fact]
    public async Task Update_consumes_success_and_forwards_current_version()
    {
        var business = new ControlledBusiness(); var id = Guid.NewGuid(); using var cancellation = new CancellationTokenSource();
        var result = Assert.IsType<OkObjectResult>(await Create(business).Update(id, new("Updated", "Todo", 8), cancellation.Token));
        Assert.Equal(Dto(business.Item), result.Value); Assert.Equal(new UpdateWorkItem("alice@example.test", id, "Updated", WorkStatus.Todo, 8), Assert.Single(business.Commands)); Assert.Equal(cancellation.Token, business.Token);
    }
    [Fact]
    public async Task Update_maps_validation_OneOf_to_400()
    {
        var business = new ControlledBusiness { UpdateResult = new ValidationError("Invalid expected version") };
        var result = Assert.IsType<BadRequestObjectResult>(await Create(business).Update(Guid.NewGuid(), new("x", "Todo", 1)));
        Assert.Contains("Invalid expected version", JsonSerializer.Serialize(result.Value)); Assert.Single(business.Commands);
    }
    [Fact]
    public async Task Update_maps_not_found_OneOf_to_404()
    {
        var business = new ControlledBusiness { UpdateResult = new NotFound() }; Assert.Equal(404, Status(await Create(business).Update(Guid.NewGuid(), new("x", "Todo", 1)))); Assert.Single(business.Commands);
    }
    [Fact]
    public async Task Update_maps_conflict_OneOf_to_409_without_retry()
    {
        var business = new ControlledBusiness { UpdateResult = new Conflict() }; Assert.Equal(409, Status(await Create(business).Update(Guid.NewGuid(), new("x", "Todo", 1)))); Assert.Single(business.Commands);
    }
    [Fact]
    public async Task Update_rejects_invalid_status_before_dispatch()
    {
        var business = new ControlledBusiness(); Assert.Equal(400, Status(await Create(business).Update(Guid.NewGuid(), new("x", "done", 1)))); Assert.Empty(business.Commands);
    }
    [Fact]
    public async Task Delete_consumes_deleted_and_returns_bodyless_204()
    {
        var business = new ControlledBusiness(); var id = Guid.NewGuid(); using var cancellation = new CancellationTokenSource();
        Assert.IsType<NoContentResult>(await Create(business).Delete(id, 8, cancellation.Token));
        Assert.Equal(new DeleteWorkItem("alice@example.test", id, 8), Assert.Single(business.Commands)); Assert.Equal(cancellation.Token, business.Token); Assert.Empty(business.Queries);
    }
    [Fact]
    public async Task Delete_maps_validation_OneOf_to_400()
    {
        var business = new ControlledBusiness { DeleteResult = new ValidationError("Invalid delete version") };
        var result = Assert.IsType<BadRequestObjectResult>(await Create(business).Delete(Guid.NewGuid(), 1));
        Assert.Contains("Invalid delete version", JsonSerializer.Serialize(result.Value)); Assert.Single(business.Commands);
    }
    [Fact]
    public async Task Delete_maps_not_found_OneOf_to_404()
    {
        var business = new ControlledBusiness { DeleteResult = new NotFound() }; Assert.Equal(404, Status(await Create(business).Delete(Guid.NewGuid(), 1))); Assert.Single(business.Commands);
    }
    [Fact]
    public async Task Delete_maps_conflict_OneOf_to_409_without_retry()
    {
        var business = new ControlledBusiness { DeleteResult = new Conflict() }; Assert.Equal(409, Status(await Create(business).Delete(Guid.NewGuid(), 1))); Assert.Single(business.Commands);
    }
    [Theory]
    [InlineData(null)][InlineData(0)][InlineData(-1)]
    public async Task Delete_rejects_absent_or_invalid_version_before_dispatch(int? version)
    {
        var business = new ControlledBusiness(); Assert.Equal(400, Status(await Create(business).Delete(Guid.NewGuid(), version))); Assert.Empty(business.Commands);
    }
    private static WorkItemsController Create(ControlledBusiness business) => new(business, business)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "alice@example.test")], "Test")) } }
    };
    private static ItemDto Dto(WorkItem item) => new(item.Id, item.Title, item.Status.ToString(), item.Version);
    private static int? Status(IActionResult result) => result switch { StatusCodeResult status => status.StatusCode, ObjectResult value => value.StatusCode, _ => null };
    private static IEnumerable<Type> ControllerTypes(Type type)
    {
        yield return type;
        foreach (var child in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            foreach (var nested in ControllerTypes(child)) yield return nested;
    }
    private static IEnumerable<MethodBase> CalledMethods(MethodInfo method)
    {
        var bytes = method.GetMethodBody()?.GetILAsByteArray();
        if (bytes is null) yield break;
        var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null)!).ToDictionary(code => unchecked((ushort)code.Value));
        for (var offset = 0; offset < bytes.Length;)
        {
            var key = (ushort)bytes[offset++];
            if (key == 0xfe) key = (ushort)(0xfe00 | bytes[offset++]);
            var code = codes[key];
            if (code == OpCodes.Call || code == OpCodes.Callvirt)
            {
                var called = method.Module.ResolveMethod(BitConverter.ToInt32(bytes, offset), method.DeclaringType?.GetGenericArguments(), method.GetGenericArguments());
                if (called is not null) yield return called;
            }
            offset += code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, offset),
                _ => 4
            };
        }
    }
    private sealed class ControlledBusiness : IWorkItemQueries, IWorkItemCommands
    {
        public WorkItem Item { get; } = new(Guid.NewGuid(), "alice@example.test", "Authoritative", WorkStatus.Done, 9);
        public List<ListWorkItems> Queries { get; } = [];
        public List<object> Commands { get; } = [];
        public CancellationToken Token;
        public OneOf<IReadOnlyList<WorkItem>, ValidationError> ListResult;
        public OneOf<WorkItem, ValidationError> CreateResult;
        public OneOf<WorkItem, ValidationError, NotFound, Conflict> UpdateResult;
        public OneOf<Deleted, ValidationError, NotFound, Conflict> DeleteResult = new Deleted();
        public ControlledBusiness() { ListResult = new[] { Item }; CreateResult = Item; UpdateResult = Item; }
        public Task<OneOf<IReadOnlyList<WorkItem>, ValidationError>> Handle(ListWorkItems query, CancellationToken ct = default) { Queries.Add(query); Token = ct; return Task.FromResult(ListResult); }
        public Task<OneOf<WorkItem, ValidationError>> Handle(CreateWorkItem command, CancellationToken ct = default) { Commands.Add(command); Token = ct; return Task.FromResult(CreateResult); }
        public Task<OneOf<WorkItem, ValidationError, NotFound, Conflict>> Handle(UpdateWorkItem command, CancellationToken ct = default) { Commands.Add(command); Token = ct; return Task.FromResult(UpdateResult); }
        public Task<OneOf<Deleted, ValidationError, NotFound, Conflict>> Handle(DeleteWorkItem command, CancellationToken ct = default) { Commands.Add(command); Token = ct; return Task.FromResult(DeleteResult); }
    }
}
