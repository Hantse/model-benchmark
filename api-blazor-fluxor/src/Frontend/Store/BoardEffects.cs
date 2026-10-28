using System.Collections.Immutable;
using Benchmark.Frontend.Api;
using Fluxor;
namespace Benchmark.Frontend.Store;
public sealed class BoardEffects(IWorkItemApi api, IState<BoardState> state)
{
    [EffectMethod] public async Task Login(LoginRequested action, IDispatcher dispatcher)
    {
        var s = state.Value;
        if (s.Pending && s.ActiveRequest != action.RequestId) return;
        try
        {
            var token = await api.LoginAsync(action.Email, action.Password);
            var after = state.Value;
            if (after.SessionVersion != s.SessionVersion || after.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new LoginSucceeded(action.RequestId, s.SessionVersion, token));
            await Load(new LoadRequested(action.RequestId), dispatcher);
        }
        catch (ApiFailureException e)
        {
            var after = state.Value;
            if (after.SessionVersion != s.SessionVersion || after.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, s.SessionVersion, e.Status, e.Message));
        }
    }
    [EffectMethod] public async Task Load(LoadRequested action, IDispatcher dispatcher)
    {
        var s = state.Value;
        if (s.AccessToken is null) return;
        if (s.Pending && s.ActiveRequest != action.RequestId) return;
        try
        {
            var items = await api.ListAsync(s.AccessToken);
            var after = state.Value;
            if (after.SessionVersion != s.SessionVersion || after.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new ItemsLoaded(action.RequestId, s.SessionVersion, items));
        }
        catch (ApiFailureException e)
        {
            var after = state.Value;
            if (after.SessionVersion != s.SessionVersion || after.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, s.SessionVersion, e.Status, e.Message));
        }
    }
    [EffectMethod] public async Task Add(AddRequested action, IDispatcher dispatcher)
    {
        var s = state.Value;
        if (s.AccessToken is null) return;
        if (s.Pending && s.ActiveRequest != action.RequestId) return;
        try
        {
            await api.AddAsync(s.AccessToken, action.Title, action.Status);
            await Refresh(action.RequestId, s.SessionVersion, dispatcher);
        }
        catch (ApiFailureException e)
        {
            var after = state.Value;
            if (after.SessionVersion != s.SessionVersion || after.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, s.SessionVersion, e.Status, e.Message));
        }
    }
    [EffectMethod] public async Task Update(UpdateRequested action, IDispatcher dispatcher)
    {
        var s = state.Value;
        if (s.AccessToken is null) return;
        if (s.Pending && s.ActiveRequest != action.RequestId) return;
        try
        {
            await api.UpdateAsync(s.AccessToken, action.Id, action.Title, action.Status, action.ExpectedVersion);
            await Refresh(action.RequestId, s.SessionVersion, dispatcher);
        }
        catch (ApiFailureException e)
        {
            var after = state.Value;
            if (after.SessionVersion != s.SessionVersion || after.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, s.SessionVersion, e.Status, e.Message));
        }
    }
    [EffectMethod] public async Task Delete(DeleteRequested action, IDispatcher dispatcher)
    {
        var s = state.Value;
        if (s.AccessToken is null) return;
        if (s.Pending && s.ActiveRequest != action.RequestId) return;
        try
        {
            await api.DeleteAsync(s.AccessToken, action.Id, action.ExpectedVersion);
            await Refresh(action.RequestId, s.SessionVersion, dispatcher);
        }
        catch (ApiFailureException e)
        {
            var after = state.Value;
            if (after.SessionVersion != s.SessionVersion || after.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, s.SessionVersion, e.Status, e.Message));
        }
    }
    private async Task Refresh(Guid requestId, long session, IDispatcher dispatcher)
    {
        try
        {
            var token = state.Value.AccessToken;
            if (token is null) return;
            var items = await api.ListAsync(token);
            var after = state.Value;
            if (after.SessionVersion != session || after.ActiveRequest != requestId) return;
            dispatcher.Dispatch(new ItemsLoaded(requestId, session, items));
        }
        catch (ApiFailureException e)
        {
            var after = state.Value;
            if (after.SessionVersion != session || after.ActiveRequest != requestId) return;
            dispatcher.Dispatch(new OperationFailed(requestId, session, e.Status, e.Message));
        }
    }
}
