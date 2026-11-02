using System.Collections.Immutable;
using Benchmark.Frontend.Api;
using Fluxor;
namespace Benchmark.Frontend.Store;
public sealed class BoardEffects(IWorkItemApi api, IState<BoardState> state)
{
    [EffectMethod]
    public async Task Login(LoginRequested action, IDispatcher dispatcher)
    {
        var current = state.Value;
        if (current.Pending && current.ActiveRequest == action.RequestId) return;
        try
        {
            var token = await api.LoginAsync(action.Email, action.Password);
            var after = state.Value;
            if (after.SessionVersion != current.SessionVersion || after.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new LoginSucceeded(action.RequestId, after.SessionVersion, token));
            dispatcher.Dispatch(new LoadRequested(action.RequestId));
        }
        catch (ApiFailureException ex)
        {
            var after = state.Value;
            if (after.SessionVersion != current.SessionVersion || after.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, after.SessionVersion, ex.Status, ex.Message));
        }
    }

    [EffectMethod]
    public async Task Load(LoadRequested action, IDispatcher dispatcher)
    {
        var current = state.Value;
        if (current.AccessToken is null) return;
        if (current.Pending && current.ActiveRequest == action.RequestId) return;
        try
        {
            var items = await api.ListAsync(current.AccessToken);
            var after = state.Value;
            if (after.SessionVersion != current.SessionVersion || after.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new ItemsLoaded(action.RequestId, after.SessionVersion, items));
        }
        catch (ApiFailureException ex)
        {
            var after = state.Value;
            if (after.SessionVersion != current.SessionVersion || after.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, after.SessionVersion, ex.Status, ex.Message));
        }
    }

    [EffectMethod]
    public async Task Add(AddRequested action, IDispatcher dispatcher)
    {
        var current = state.Value;
        if (current.AccessToken is null) return;
        if (current.Pending && current.ActiveRequest == action.RequestId) return;
        try
        {
            await api.AddAsync(current.AccessToken, action.Title, action.Status);
            var after = state.Value;
            if (after.SessionVersion != current.SessionVersion || after.ActiveRequest != action.RequestId) return;
            var items = await api.ListAsync(current.AccessToken);
            var after2 = state.Value;
            if (after2.SessionVersion != current.SessionVersion || after2.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new ItemsLoaded(action.RequestId, after2.SessionVersion, items));
        }
        catch (ApiFailureException ex)
        {
            var after = state.Value;
            if (after.SessionVersion != current.SessionVersion || after.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, after.SessionVersion, ex.Status, ex.Message));
        }
    }

    [EffectMethod]
    public async Task Update(UpdateRequested action, IDispatcher dispatcher)
    {
        var current = state.Value;
        if (current.AccessToken is null) return;
        if (current.Pending && current.ActiveRequest == action.RequestId) return;
        try
        {
            await api.UpdateAsync(current.AccessToken, action.Id, action.Title, action.Status, action.ExpectedVersion);
            var after = state.Value;
            if (after.SessionVersion != current.SessionVersion || after.ActiveRequest != action.RequestId) return;
            var items = await api.ListAsync(current.AccessToken);
            var after2 = state.Value;
            if (after2.SessionVersion != current.SessionVersion || after2.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new ItemsLoaded(action.RequestId, after2.SessionVersion, items));
        }
        catch (ApiFailureException ex)
        {
            var after = state.Value;
            if (after.SessionVersion != current.SessionVersion || after.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, after.SessionVersion, ex.Status, ex.Message));
        }
    }

    [EffectMethod]
    public async Task Delete(DeleteRequested action, IDispatcher dispatcher)
    {
        var current = state.Value;
        if (current.AccessToken is null) return;
        if (current.Pending && current.ActiveRequest == action.RequestId) return;
        try
        {
            await api.DeleteAsync(current.AccessToken, action.Id, action.ExpectedVersion);
            var after = state.Value;
            if (after.SessionVersion != current.SessionVersion || after.ActiveRequest != action.RequestId) return;
            var items = await api.ListAsync(current.AccessToken);
            var after2 = state.Value;
            if (after2.SessionVersion != current.SessionVersion || after2.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new ItemsLoaded(action.RequestId, after2.SessionVersion, items));
        }
        catch (ApiFailureException ex)
        {
            var after = state.Value;
            if (after.SessionVersion != current.SessionVersion || after.ActiveRequest != action.RequestId) return;
            dispatcher.Dispatch(new OperationFailed(action.RequestId, after.SessionVersion, ex.Status, ex.Message));
        }
    }
}
