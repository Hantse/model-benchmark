using Benchmark.Frontend.Api;
using Benchmark.Frontend.Store;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Benchmark.Frontend.Tests;
public sealed class StoreHarness : IDisposable
{
    private readonly ServiceProvider provider;
    public FakeApi Api {get;}=new();
    public IState<BoardState> State {get;}
    public IDispatcher Dispatcher {get;}
    public StoreHarness(){var services=new ServiceCollection();services.AddSingleton<IWorkItemApi>(Api);services.AddLogging();services.AddFluxor(o=>o.ScanAssemblies(typeof(BoardState).Assembly).WithLifetime(StoreLifetime.Scoped));provider=services.BuildServiceProvider();State=provider.GetRequiredService<IState<BoardState>>();Dispatcher=provider.GetRequiredService<IDispatcher>();provider.GetRequiredService<IStore>().InitializeAsync().GetAwaiter().GetResult();}
    public async Task Login(){Dispatcher.Dispatch(new LoginRequested(Guid.NewGuid(),"alice@example.test","Alice!234"));await Until(()=>State.Value.AccessToken=="test-token"&&!State.Value.Pending);}
    public static async Task Until(Func<bool> predicate){using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(2));while(!predicate()){if(budget.IsCancellationRequested)Assert.Fail("Expected Fluxor transition was not observed.");await Task.Delay(10);}}
    public void Dispose()=>provider.Dispose();
}
