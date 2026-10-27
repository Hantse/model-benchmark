using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using Benchmark.Frontend.Api;
using Benchmark.Frontend.Store;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Benchmark.Frontend.Tests;

public sealed class FluxorContractTests
{
    private static BoardState Pending(Guid request, long session = 7) =>
        new("current-token", session, [new(Guid.NewGuid(), "Current private item", "Todo", 2)], "All", true, request, null);

    [Fact]
    public void Items_for_a_different_request_in_the_same_session_are_ignored()
    {
        var before = Pending(Guid.NewGuid());
        var after = Reducers.Loaded(before, new(Guid.NewGuid(), before.SessionVersion,
            [new(Guid.NewGuid(), "Unrelated result", "Done", 9)]));
        Assert.Equal(before, after);
    }

    [Fact]
    public void Login_success_for_a_different_request_in_the_same_session_is_ignored()
    {
        var before = Pending(Guid.NewGuid()) with { AccessToken = null, Items = [] };
        Assert.Equal(before, Reducers.LoginSuccess(before, new(Guid.NewGuid(), before.SessionVersion, "unrelated-token")));
    }

    [Fact]
    public void Failure_for_a_different_request_does_not_finish_the_active_operation()
    {
        var before = Pending(Guid.NewGuid());
        Assert.Equal(before, Reducers.Failed(before, new(Guid.NewGuid(), before.SessionVersion, 409, "Unrelated failure")));
    }

    [Fact]
    public void Old_session_401_does_not_clear_a_new_session_even_if_request_id_matches()
    {
        var request = Guid.NewGuid();
        var before = Pending(request, session: 9);
        Assert.Equal(before, Reducers.Failed(before, new(request, 8, 401, "Old token expired")));
    }

    [Fact]
    public async Task Anonymous_requests_do_not_call_the_HTTP_adapter()
    {
        using var h = new StoreHarness();
        h.Dispatcher.Dispatch(new LoadRequested(Guid.NewGuid()));
        h.Dispatcher.Dispatch(new AddRequested(Guid.NewGuid(), "Private", "Todo"));
        h.Dispatcher.Dispatch(new UpdateRequested(Guid.NewGuid(), Guid.NewGuid(), "Private", "Done", 1));
        h.Dispatcher.Dispatch(new DeleteRequested(Guid.NewGuid(), Guid.NewGuid(), 1));
        await Task.Delay(50);
        Assert.Empty(h.Api.Calls);
        Assert.Equal(new BoardState(), h.State.Value);
    }

    [Fact]
    public async Task Pending_read_suppresses_mutations_but_allows_filter_changes()
    {
        using var h = new StoreHarness();
        var item = new WorkItemView(Guid.NewGuid(), "Keep", "Todo", 3);
        h.Api.Items = [item];
        await h.Login();
        h.Api.ListGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = Guid.NewGuid();
        h.Dispatcher.Dispatch(new LoadRequested(request));
        h.Dispatcher.Dispatch(new AddRequested(Guid.NewGuid(), "Ignored", "Todo"));
        h.Dispatcher.Dispatch(new UpdateRequested(Guid.NewGuid(), item.Id, "Ignored", "Done", 3));
        h.Dispatcher.Dispatch(new DeleteRequested(Guid.NewGuid(), item.Id, 3));
        h.Dispatcher.Dispatch(new FilterChanged("Done"));
        Assert.Equal(request, h.State.Value.ActiveRequest);
        Assert.True(h.State.Value.Pending);
        Assert.Equal("Done", h.State.Value.Filter);
        Assert.DoesNotContain(h.Api.Calls, c => c.Method is "POST" or "PUT" or "DELETE");
        h.Api.ListGate.SetResult([item]);
        await StoreHarness.Until(() => !h.State.Value.Pending);
        Assert.Equal("Done", h.State.Value.Filter);
        Assert.Equal(item, Assert.Single(h.State.Value.Items));
    }

    [Fact]
    public async Task Duplicate_login_while_pending_keeps_one_request_and_one_API_call()
    {
        using var h = new StoreHarness();
        h.Api.LoginGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = Guid.NewGuid();
        h.Dispatcher.Dispatch(new LoginRequested(request, "alice@example.test", "Alice!234"));
        var session = h.State.Value.SessionVersion;
        h.Dispatcher.Dispatch(new LoginRequested(Guid.NewGuid(), "bob@example.test", "Bob!234"));
        Assert.True(h.State.Value.Pending);
        Assert.Equal(request, h.State.Value.ActiveRequest);
        Assert.Equal(session, h.State.Value.SessionVersion);
        Assert.Single(h.Api.Calls, c => c.Method == "LOGIN");
        h.Api.LoginGate.SetResult("test-token");
        await StoreHarness.Until(() => h.State.Value.AccessToken == "test-token" && !h.State.Value.Pending);
    }

    [Fact]
    public async Task Late_read_401_after_relogin_does_not_log_out_the_new_session()
    {
        using var h = new StoreHarness();
        await h.Login();
        var oldRead = new TaskCompletionSource<ImmutableArray<WorkItemView>>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Api.ListGate = oldRead;
        h.Dispatcher.Dispatch(new LoadRequested(Guid.NewGuid()));
        Assert.True(h.State.Value.Pending);
        h.Dispatcher.Dispatch(new LogoutRequested());
        h.Api.ListGate = null;
        h.Api.Items = [new(Guid.NewGuid(), "New session private item", "Done", 5)];
        await h.Login();
        var newSession = h.State.Value.SessionVersion;
        oldRead.SetException(new ApiFailureException(401, "Old request expired"));
        await Task.Delay(50);
        Assert.Equal(newSession, h.State.Value.SessionVersion);
        Assert.Equal("test-token", h.State.Value.AccessToken);
        Assert.Equal("New session private item", Assert.Single(h.State.Value.Items).Title);
        Assert.Null(h.State.Value.Error);
    }

    [Fact]
    public async Task Late_mutation_401_after_relogin_does_not_log_out_the_new_session()
    {
        using var h = new StoreHarness();
        await h.Login();
        var oldAdd = new TaskCompletionSource<WorkItemView>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Api.AddGate = oldAdd;
        h.Dispatcher.Dispatch(new AddRequested(Guid.NewGuid(), "Old mutation", "Todo"));
        Assert.True(h.State.Value.Pending);
        h.Dispatcher.Dispatch(new LogoutRequested());
        await h.Login();
        var newSession = h.State.Value.SessionVersion;
        var readsBefore = h.Api.Calls.Count(c => c.Method == "GET");
        oldAdd.SetException(new ApiFailureException(401, "Old mutation expired"));
        await Task.Delay(50);
        Assert.Equal(newSession, h.State.Value.SessionVersion);
        Assert.Equal("test-token", h.State.Value.AccessToken);
        Assert.False(h.State.Value.Pending);
        Assert.Null(h.State.Value.Error);
        Assert.Equal(readsBefore, h.Api.Calls.Count(c => c.Method == "GET"));
    }

    [Fact]
    public async Task Successful_mutation_with_failed_refresh_preserves_previous_snapshot_without_retry()
    {
        using var h = new StoreHarness();
        var existing = new WorkItemView(Guid.NewGuid(), "Previous snapshot", "Todo", 2);
        h.Api.Items = [existing];
        await h.Login();
        h.Api.AddGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Dispatcher.Dispatch(new AddRequested(Guid.NewGuid(), "Saved on server", "Done"));
        Assert.True(h.State.Value.Pending);
        h.Api.FailureStatus = 500;
        h.Api.AddGate.SetResult(new(Guid.NewGuid(), "Saved on server", "Done", 1));
        await StoreHarness.Until(() => !h.State.Value.Pending && h.State.Value.Error != null);
        Assert.Equal(existing, Assert.Single(h.State.Value.Items));
        Assert.Equal("test-token", h.State.Value.AccessToken);
        Assert.Single(h.Api.Calls, c => c.Method == "POST");
        Assert.Equal(2, h.Api.Calls.Count(c => c.Method == "GET"));
    }

    [Fact]
    public async Task Filters_and_logout_remain_independent_between_real_scoped_stores()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWorkItemApi>(new FakeApi());
        services.AddLogging();
        services.AddFluxor(o => o.ScanAssemblies(typeof(BoardState).Assembly).WithLifetime(StoreLifetime.Scoped));
        using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        foreach (var scope in new[] { first, second })
        {
            await scope.ServiceProvider.GetRequiredService<IStore>().InitializeAsync();
            scope.ServiceProvider.GetRequiredService<IDispatcher>()
                .Dispatch(new LoginRequested(Guid.NewGuid(), "alice@example.test", "Alice!234"));
        }
        var firstState = first.ServiceProvider.GetRequiredService<IState<BoardState>>();
        var secondState = second.ServiceProvider.GetRequiredService<IState<BoardState>>();
        await StoreHarness.Until(() => firstState.Value.AccessToken != null && secondState.Value.AccessToken != null);
        first.ServiceProvider.GetRequiredService<IDispatcher>().Dispatch(new FilterChanged("Done"));
        Assert.Equal("Done", firstState.Value.Filter);
        Assert.Equal("All", secondState.Value.Filter);
        first.ServiceProvider.GetRequiredService<IDispatcher>().Dispatch(new LogoutRequested());
        Assert.Null(firstState.Value.AccessToken);
        Assert.Equal("test-token", secondState.Value.AccessToken);
    }

    [Fact]
    public void Frontend_does_not_call_locking_or_blocking_primitives()
    {
        // Inspect compiled calls, including compiler-generated async state machines.
        // Merely registering Fluxor while hiding a custom lock-based store is insufficient.
        var forbidden = typeof(BoardState).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Cast<MethodBase>().Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)))
            .SelectMany(Calls)
            .Where(method => method.DeclaringType?.FullName is "System.Threading.Monitor" or "System.Threading.SemaphoreSlim"
                or "System.Threading.Semaphore" or "System.Threading.ReaderWriterLock" or "System.Threading.ReaderWriterLockSlim"
                or "System.Threading.Mutex" or "System.Threading.SpinLock" or "System.Threading.SpinWait" or "System.Threading.Lock"
                or "System.Threading.ManualResetEvent" or "System.Threading.ManualResetEventSlim" or "System.Threading.AutoResetEvent"
                || method.DeclaringType == typeof(Thread) && method.Name is nameof(Thread.Sleep) or nameof(Thread.SpinWait)
                || method.DeclaringType == typeof(Task) && method.Name is nameof(Task.Wait) or nameof(Task.WaitAll) or nameof(Task.WaitAny)
                || method.DeclaringType?.IsGenericType == true && method.DeclaringType.GetGenericTypeDefinition() == typeof(Task<>) && method.Name == "get_Result")
            .Select(method => $"{method.DeclaringType!.FullName}.{method.Name}")
            .Distinct().ToArray();
        Assert.Empty(forbidden);
    }

    private static readonly IReadOnlyDictionary<short, OpCode> OpCodesByValue = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!).ToDictionary(code => code.Value);

    private static IEnumerable<MethodBase> Calls(MethodBase method)
    {
        var bytes = method.GetMethodBody()?.GetILAsByteArray();
        if (bytes is null) yield break;
        for (var offset = 0; offset < bytes.Length;)
        {
            var code = OpCodesByValue[bytes[offset] == 0xfe
                ? unchecked((short)(0xfe00 | bytes[++offset]))
                : bytes[offset]];
            offset++;
            if (code.OperandType == OperandType.InlineMethod)
            {
                var target = method.Module.ResolveMethod(BitConverter.ToInt32(bytes, offset),
                    method.DeclaringType?.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);
                if (target is not null) yield return target;
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
}
