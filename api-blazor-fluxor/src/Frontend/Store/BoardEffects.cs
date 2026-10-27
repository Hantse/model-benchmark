using Benchmark.Frontend.Api;
using Fluxor;
namespace Benchmark.Frontend.Store;
public sealed class BoardEffects(IWorkItemApi api, IState<BoardState> state)
{
    [EffectMethod] public async Task Login(LoginRequested action, IDispatcher dispatcher)
    {
        var session = state.Value.SessionVersion;
        try { var token = await api.LoginAsync(action.Email, action.Password); if (state.Value.SessionVersion != session) return; dispatcher.Dispatch(new LoginSucceeded(action.RequestId, session, token)); dispatcher.Dispatch(new LoadRequested(Guid.NewGuid())); }
        catch (ApiFailureException e) { if (state.Value.SessionVersion != session) return; dispatcher.Dispatch(new OperationFailed(action.RequestId, session, e.Status, e.Message)); }
    }
    [EffectMethod] public async Task Load(LoadRequested action, IDispatcher dispatcher)
    {
        var s = state.Value; if (s.AccessToken is null) return; var session = s.SessionVersion; var token = s.AccessToken;
        try { var items = await api.ListAsync(token); if (state.Value.SessionVersion != session) return; dispatcher.Dispatch(new ItemsLoaded(action.RequestId, session, items)); }
        catch (ApiFailureException e) { if (state.Value.SessionVersion != session) return; dispatcher.Dispatch(new OperationFailed(action.RequestId, session, e.Status, e.Message)); }
    }
    [EffectMethod] public async Task Add(AddRequested action, IDispatcher dispatcher)
    {
        var s = state.Value; if (s.AccessToken is null) return; var session = s.SessionVersion; var token = s.AccessToken;
        try { await api.AddAsync(token, action.Title, action.Status); await Reload(action.RequestId, session, token, dispatcher); }
        catch (ApiFailureException e) { if (state.Value.SessionVersion != session) return; dispatcher.Dispatch(new OperationFailed(action.RequestId, session, e.Status, e.Message)); }
    }
    [EffectMethod] public async Task Update(UpdateRequested action, IDispatcher dispatcher)
    {
        var s = state.Value; if (s.AccessToken is null) return; var session = s.SessionVersion; var token = s.AccessToken;
        try { await api.UpdateAsync(token, action.Id, action.Title, action.Status, action.ExpectedVersion); await Reload(action.RequestId, session, token, dispatcher); }
        catch (ApiFailureException e) { if (state.Value.SessionVersion != session) return; dispatcher.Dispatch(new OperationFailed(action.RequestId, session, e.Status, e.Message)); }
    }
    [EffectMethod] public async Task Delete(DeleteRequested action, IDispatcher dispatcher)
    {
        var s = state.Value; if (s.AccessToken is null) return; var session = s.SessionVersion; var token = s.AccessToken;
        try { await api.DeleteAsync(token, action.Id, action.ExpectedVersion); await Reload(action.RequestId, session, token, dispatcher); }
        catch (ApiFailureException e) { if (state.Value.SessionVersion != session) return; dispatcher.Dispatch(new OperationFailed(action.RequestId, session, e.Status, e.Message)); }
    }
    private async Task Reload(Guid requestId, long session, string token, IDispatcher dispatcher)
    {
        try { var items = await api.ListAsync(token); if (state.Value.SessionVersion != session) return; dispatcher.Dispatch(new ItemsLoaded(requestId, session, items)); }
        catch (ApiFailureException e) { if (state.Value.SessionVersion != session) return; dispatcher.Dispatch(new OperationFailed(requestId, session, e.Status, e.Message)); }
    }
}
