using System.Collections.Immutable;
using Benchmark.Frontend.Api;
using Fluxor;
namespace Benchmark.Frontend.Store;
public sealed class BoardEffects(IWorkItemApi api, IState<BoardState> state)
{
    [EffectMethod] public async Task Login(LoginRequested action, IDispatcher dispatcher)
    {
        var s = state.Value;
        if (s.Pending) return;
        var session = s.SessionVersion + 1;
        try
        {
            var token = await api.LoginAsync(action.Email, action.Password);
            if (state.Value.SessionVersion != session) return;
            dispatcher.Dispatch(new LoginSucceeded(action.RequestId, session, token));
            dispatcher.Dispatch(new LoadRequested(action.RequestId));
        }
        catch (ApiFailureException ex)
        {
            if (state.Value.SessionVersion != session) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, session, ex.Status, ex.Message));
        }
    }
    [EffectMethod] public async Task Load(LoadRequested action, IDispatcher dispatcher)
    {
        var s = state.Value;
        if (s.AccessToken is null || s.Pending) return;
        var session = s.SessionVersion;
        try
        {
            var items = await api.ListAsync(s.AccessToken);
            if (state.Value.SessionVersion != session) return;
            dispatcher.Dispatch(new ItemsLoaded(action.RequestId, session, items));
        }
        catch (ApiFailureException ex)
        {
            if (state.Value.SessionVersion != session) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, session, ex.Status, ex.Message));
        }
    }
    [EffectMethod] public async Task Add(AddRequested action, IDispatcher dispatcher)
    {
        var s = state.Value;
        if (s.AccessToken is null || s.Pending) return;
        var session = s.SessionVersion;
        try
        {
            await api.AddAsync(s.AccessToken, action.Title, action.Status);
            if (state.Value.SessionVersion != session) return;
            var items = await api.ListAsync(s.AccessToken);
            if (state.Value.SessionVersion != session) return;
            dispatcher.Dispatch(new ItemsLoaded(action.RequestId, session, items));
        }
        catch (ApiFailureException ex)
        {
            if (state.Value.SessionVersion != session) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, session, ex.Status, ex.Message));
        }
    }
    [EffectMethod] public async Task Update(UpdateRequested action, IDispatcher dispatcher)
    {
        var s = state.Value;
        if (s.AccessToken is null || s.Pending) return;
        var session = s.SessionVersion;
        try
        {
            await api.UpdateAsync(s.AccessToken, action.Id, action.Title, action.Status, action.ExpectedVersion);
            if (state.Value.SessionVersion != session) return;
            var items = await api.ListAsync(s.AccessToken);
            if (state.Value.SessionVersion != session) return;
            dispatcher.Dispatch(new ItemsLoaded(action.RequestId, session, items));
        }
        catch (ApiFailureException ex)
        {
            if (state.Value.SessionVersion != session) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, session, ex.Status, ex.Message));
        }
    }
    [EffectMethod] public async Task Delete(DeleteRequested action, IDispatcher dispatcher)
    {
        var s = state.Value;
        if (s.AccessToken is null || s.Pending) return;
        var session = s.SessionVersion;
        try
        {
            await api.DeleteAsync(s.AccessToken, action.Id, action.ExpectedVersion);
            if (state.Value.SessionVersion != session) return;
            var items = await api.ListAsync(s.AccessToken);
            if (state.Value.SessionVersion != session) return;
            dispatcher.Dispatch(new ItemsLoaded(action.RequestId, session, items));
        }
        catch (ApiFailureException ex)
        {
            if (state.Value.SessionVersion != session) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, session, ex.Status, ex.Message));
        }
    }
}
