using Benchmark.Frontend.Api;
using Fluxor;
namespace Benchmark.Frontend.Store;
// Implement asynchronous HTTP effects, including stale-session rejection.
public sealed class BoardEffects(IWorkItemApi api, IState<BoardState> state)
{
    [EffectMethod] public Task Login(LoginRequested action, IDispatcher dispatcher) => Task.CompletedTask;
    [EffectMethod] public Task Load(LoadRequested action, IDispatcher dispatcher) => Task.CompletedTask;
    [EffectMethod] public Task Add(AddRequested action, IDispatcher dispatcher) => Task.CompletedTask;
    [EffectMethod] public Task Update(UpdateRequested action, IDispatcher dispatcher) => Task.CompletedTask;
    [EffectMethod] public Task Delete(DeleteRequested action, IDispatcher dispatcher) => Task.CompletedTask;
}
