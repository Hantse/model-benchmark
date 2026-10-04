using System.Collections.Immutable;
using Benchmark.Frontend.Api;
using Fluxor;
namespace Benchmark.Frontend.Store;
[FeatureState]
public sealed record BoardState(string? AccessToken, long SessionVersion, ImmutableArray<WorkItemView> Items, string Filter, bool Pending, Guid? ActiveRequest, string? Error)
{
    public BoardState() : this(null, 0, [], "All", false, null, null) { }
}
