using System.Collections.Immutable;
using Fluxor;
namespace Benchmark.Frontend.Store;
public static class Reducers
{
    [ReducerMethod] public static BoardState Login(BoardState state, LoginRequested action)
        => state with { AccessToken = null, SessionVersion = state.SessionVersion + 1, Items = [], Filter = "All", Pending = true, ActiveRequest = action.RequestId, Error = null };
    [ReducerMethod] public static BoardState LoginSuccess(BoardState state, LoginSucceeded action)
        => state.SessionVersion != action.SessionVersion || state.ActiveRequest != action.RequestId
            ? state
            : state with { AccessToken = action.AccessToken, Pending = false, ActiveRequest = null, Error = null };
    [ReducerMethod] public static BoardState Load(BoardState state, LoadRequested action)
        => state.AccessToken is null || state.Pending
            ? state
            : state with { Pending = true, ActiveRequest = action.RequestId, Error = null };
    [ReducerMethod] public static BoardState Add(BoardState state, AddRequested action)
        => state.AccessToken is null || state.Pending
            ? state
            : state with { Pending = true, ActiveRequest = action.RequestId, Error = null };
    [ReducerMethod] public static BoardState Update(BoardState state, UpdateRequested action)
        => state.AccessToken is null || state.Pending
            ? state
            : state with { Pending = true, ActiveRequest = action.RequestId, Error = null };
    [ReducerMethod] public static BoardState Delete(BoardState state, DeleteRequested action)
        => state.AccessToken is null || state.Pending
            ? state
            : state with { Pending = true, ActiveRequest = action.RequestId, Error = null };
    [ReducerMethod] public static BoardState Loaded(BoardState state, ItemsLoaded action)
        => state.SessionVersion != action.SessionVersion || state.ActiveRequest != action.RequestId
            ? state
            : state with { Items = action.Items, Pending = false, ActiveRequest = null, Error = null };
    [ReducerMethod] public static BoardState Failed(BoardState state, OperationFailed action)
    {
        if (state.SessionVersion != action.SessionVersion || state.ActiveRequest != action.RequestId) return state;
        if (action.Status == 401) return state with { AccessToken = null, Items = [], Pending = false, ActiveRequest = null, Error = action.Message };
        return state with { Pending = false, ActiveRequest = null, Error = action.Message };
    }
    [ReducerMethod] public static BoardState Filter(BoardState state, FilterChanged action) => state with { Filter = action.Filter };
    [ReducerMethod] public static BoardState Logout(BoardState state, LogoutRequested action)
        => state with { AccessToken = null, SessionVersion = state.SessionVersion + 1, Items = [], Filter = "All", Pending = false, ActiveRequest = null, Error = null };
}
