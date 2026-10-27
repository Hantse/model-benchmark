using Benchmark.Business;
using Benchmark.Domain;
using Benchmark.Host.Controllers;
using Benchmark.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using System.Xml.Linq;
using Xunit;
namespace Benchmark.Tests;

public sealed class LayerTests
{
    [Fact]
    public void Domain_has_no_project_transport_storage_or_OneOf_dependencies()
    {
        var names = typeof(WorkItem).Assembly.GetReferencedAssemblies().Select(x => x.Name!);
        Assert.DoesNotContain(names, x => x.StartsWith("Benchmark.") || x.StartsWith("Microsoft.AspNetCore") || x.StartsWith("Microsoft.Data.Sqlite") || x == "OneOf");
    }
    [Fact]
    public void Infrastructure_service_and_repository_do_not_reference_Business_Host_or_HTTP()
    {
        var names = typeof(WorkItemService).Assembly.GetReferencedAssemblies().Select(x => x.Name!);
        Assert.DoesNotContain(names, x => x is "Benchmark.Business" or "Benchmark.Host" or "Benchmark.Frontend" || x.StartsWith("Microsoft.AspNetCore"));
    }
    [Fact]
    public void Business_depends_on_infrastructure_services_without_HTTP_or_SQLite()
    {
        var names = typeof(WorkItemCommands).Assembly.GetReferencedAssemblies().Select(x => x.Name!).ToArray();
        Assert.Contains("Benchmark.Infrastructure", names);
        Assert.DoesNotContain(names, x => x is "Benchmark.Host" or "Benchmark.Frontend" || x.StartsWith("Microsoft.AspNetCore") || x.StartsWith("Microsoft.Data.Sqlite"));
    }
    [Fact]
    public void Source_project_references_follow_the_four_backend_layers()
    {
        var expected = new Dictionary<string, string[]> { ["Domain"] = [], ["Infrastructure"] = ["Domain"], ["Business"] = ["Domain", "Infrastructure"], ["Host"] = ["Domain", "Infrastructure", "Business"], ["Frontend"] = [] };
        foreach (var pair in expected)
        {
            var document = XDocument.Load(Path.Combine(Root(), "src", pair.Key, pair.Key + ".csproj"));
            var actual = document.Descendants("ProjectReference").Select(x => Path.GetFileNameWithoutExtension(x.Attribute("Include")!.Value));
            Assert.Equal(pair.Value.Order(), actual.Order());
        }
    }
    [Fact]
    public void Work_item_controller_receives_CQRS_ports_and_no_repository_or_service()
    {
        var parameters = typeof(WorkItemsController).GetConstructors().Single().GetParameters().Select(x => x.ParameterType).ToArray();
        Assert.Contains(typeof(IWorkItemQueries), parameters); Assert.Contains(typeof(IWorkItemCommands), parameters);
        Assert.DoesNotContain(typeof(IWorkItemService), parameters); Assert.DoesNotContain(typeof(IWorkItemRepository), parameters);
        Assert.True(typeof(ControllerBase).IsAssignableFrom(typeof(WorkItemsController)));
    }
    public static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Benchmark.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Tester root missing.");
    }
}
