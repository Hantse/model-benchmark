using System.Collections.Immutable;
using Benchmark.Frontend.Api;
using Fluxor;
namespace Benchmark.Frontend.Store;
public sealed class BoardEffects(IWorkItemApi api, IState<BoardState> state)
{
    [EffectMethod] public async Task Login(LoginRequested action, IDispatcher dispatcher)
    {
        var session = state.Value.SessionVersion;
        try
        {
            var token = await api.LoginAsync(action.Email, action.Password);
            if (state.Value.SessionVersion != session || state.Value.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new LoginSucceeded(action.RequestId, session, token));
            dispatcher.Dispatch(new LoadRequested(action.RequestId));
        }
        catch (ApiFailureException ex)
        {
            if (state.Value.SessionVersion != session || state.Value.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, session, ex.Status, ex.Message));
        }
    }
    [EffectMethod] public async Task Load(LoadRequested action, IDispatcher dispatcher)
    {
        var current = state.Value;
        if (current.AccessToken is null || current.ActiveRequest != action.RequestId) return;
        var session = current.SessionVersion;
        try
        {
            var items = await api.ListAsync(current.AccessToken);
            if (state.Value.SessionVersion != session || state.Value.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new ItemsLoaded(action.RequestId, session, items));
        }
        catch (ApiFailureException ex)
        {
            if (state.Value.SessionVersion != session || state.Value.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, session, ex.Status, ex.Message));
        }
    }
    [EffectMethod] public async Task Add(AddRequested action, IDispatcher dispatcher)
    {
        var current = state.Value;
        if (current.AccessToken is null || current.ActiveRequest != action.RequestId) return;
        var session = current.SessionVersion;
        try
        {
            await api.AddAsync(current.AccessToken, action.Title, action.Status);
            if (state.Value.SessionVersion != session || state.Value.ActiveRequest != action.RequestId) return;
            var items = await api.ListAsync(current.AccessToken);
            if (state.Value.SessionVersion != session || state.Value.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new ItemsLoaded(action.RequestId, session, items));
        }
        catch (ApiFailureException ex)
        {
            if (state.Value.SessionVersion != session || state.Value.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, session, ex.Status, ex.Message));
        }
    }
    [EffectMethod] public async Task Update(UpdateRequested action, IDispatcher dispatcher)
    {
        var current = state.Value;
        if (current.AccessToken is null || current.ActiveRequest != action.RequestId) return;
        var session = current.SessionVersion;
        try
        {
            await api.UpdateAsync(current.AccessToken, action.Id, action.Title, action.Status, action.ExpectedVersion);
            if (state.Value.SessionVersion != session || state.Value.ActiveRequest != action.RequestId) return;
            var items = await api.ListAsync(current.AccessToken);
            if (state.Value.SessionVersion != session || state.Value.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new ItemsLoaded(action.RequestId, session, items));
        }
        catch (ApiFailureException ex)
        {
            if (state.Value.SessionVersion != session || state.Value.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, session, ex.Status, ex.Message));
        }
    }
    [EffectMethod] public async Task Delete(DeleteRequested action, IDispatcher dispatcher)
    {
        var current = state.Value;
        if (current.AccessToken is null || current.ActiveRequest != action.RequestId) return;
        var session = current.SessionVersion;
        try
        {
            await api.DeleteAsync(current.AccessToken, action.Id, action.ExpectedVersion);
            if (state.Value.SessionVersion != session || state.Value.ActiveRequest != action.RequestId) return;
            var items = await api.ListAsync(current.AccessToken);
            if (state.Value.SessionVersion != session || state.Value.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new ItemsLoaded(action.RequestId, session, items));
        }
        catch (ApiFailureException ex)
        {
            if (state.Value.SessionVersion != session || state.Value.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, session, ex.Status, ex.Message));
        }
    }
}
