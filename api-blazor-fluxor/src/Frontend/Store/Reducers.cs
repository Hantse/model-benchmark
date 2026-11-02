using System.Collections.Immutable;
using Fluxor;
namespace Benchmark.Frontend.Store;
public static class Reducers
{
    [ReducerMethod]
    public static BoardState Login(BoardState state, LoginRequested action)
    {
        if (state.Pending && state.ActiveRequest == action.RequestId) return state;
        return state with { AccessToken = null, SessionVersion = state.SessionVersion + 1, Items = [], Filter = "All", Pending = true, ActiveRequest = action.RequestId, Error = null };
    }

    [ReducerMethod]
    public static BoardState LoginSuccess(BoardState state, LoginSucceeded action)
    {
        if (state.ActiveRequest != action.RequestId || state.SessionVersion != action.SessionVersion) return state;
        return state with { AccessToken = action.AccessToken, Pending = false, ActiveRequest = null, Error = null };
    }

    [ReducerMethod]
    public static BoardState Load(BoardState state, LoadRequested action)
    {
        if (state.AccessToken is null) return state;
        if (state.Pending) return state;
        return state with { Pending = true, ActiveRequest = action.RequestId };
    }

    [ReducerMethod]
    public static BoardState Add(BoardState state, AddRequested action)
    {
        if (state.AccessToken is null) return state;
        if (state.Pending) return state;
        return state with { Pending = true, ActiveRequest = action.RequestId };
    }

    [ReducerMethod]
    public static BoardState Update(BoardState state, UpdateRequested action)
    {
        if (state.AccessToken is null) return state;
        if (state.Pending) return state;
        return state with { Pending = true, ActiveRequest = action.RequestId };
    }

    [ReducerMethod]
    public static BoardState Delete(BoardState state, DeleteRequested action)
    {
        if (state.AccessToken is null) return state;
        if (state.Pending) return state;
        return state with { Pending = true, ActiveRequest = action.RequestId };
    }

    [ReducerMethod]
    public static BoardState Loaded(BoardState state, ItemsLoaded action)
    {
        if (state.ActiveRequest != action.RequestId || state.SessionVersion != action.SessionVersion) return state;
        return state with { Items = action.Items, Pending = false, ActiveRequest = null, Error = null };
    }

    [ReducerMethod]
    public static BoardState Failed(BoardState state, OperationFailed action)
    {
        if (state.ActiveRequest != action.RequestId || state.SessionVersion != action.SessionVersion) return state;
        if (action.Status == 401)
            return state with { AccessToken = null, Items = [], Pending = false, ActiveRequest = null, Error = action.Message, SessionVersion = state.SessionVersion + 1 };
        return state with { Pending = false, ActiveRequest = null, Error = action.Message };
    }

    [ReducerMethod]
    public static BoardState Filter(BoardState state, FilterChanged action)
    {
        return state with { Filter = action.Filter };
    }

    [ReducerMethod]
    public static BoardState Logout(BoardState state, LogoutRequested action)
    {
        return state with { AccessToken = null, Items = [], Filter = "All", Pending = false, ActiveRequest = null, Error = null, SessionVersion = state.SessionVersion + 1 };
    }
}
