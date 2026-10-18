using Fluxor;
namespace Benchmark.Frontend.Store;
public static class Reducers
{
    [ReducerMethod] public static BoardState Login(BoardState state, LoginRequested action)
        => state with { SessionVersion = state.SessionVersion + 1, AccessToken = null, Items = [], Error = null, Pending = true, ActiveRequest = action.RequestId };

    [ReducerMethod] public static BoardState LoginSuccess(BoardState state, LoginSucceeded action)
        => action.SessionVersion != state.SessionVersion || action.RequestId != state.ActiveRequest
            ? state
            : state with { AccessToken = action.AccessToken, Pending = false, ActiveRequest = null };

    [ReducerMethod] public static BoardState Load(BoardState state, LoadRequested action)
        => state.AccessToken is null || state.Pending
            ? state
            : state with { Pending = true, ActiveRequest = action.RequestId };

    [ReducerMethod] public static BoardState Add(BoardState state, AddRequested action)
        => state.AccessToken is null || state.Pending
            ? state
            : state with { Pending = true, ActiveRequest = action.RequestId };

    [ReducerMethod] public static BoardState Update(BoardState state, UpdateRequested action)
        => state.AccessToken is null || state.Pending
            ? state
            : state with { Pending = true, ActiveRequest = action.RequestId };

    [ReducerMethod] public static BoardState Delete(BoardState state, DeleteRequested action)
        => state.AccessToken is null || state.Pending
            ? state
            : state with { Pending = true, ActiveRequest = action.RequestId };

    [ReducerMethod] public static BoardState Loaded(BoardState state, ItemsLoaded action)
        => action.SessionVersion != state.SessionVersion || action.RequestId != state.ActiveRequest
            ? state
            : state with { Items = action.Items, Pending = false, ActiveRequest = null, Error = null };

    [ReducerMethod] public static BoardState Failed(BoardState state, OperationFailed action)
    {
        if (action.SessionVersion != state.SessionVersion || action.RequestId != state.ActiveRequest) return state;
        if (action.Status == 401)
            return state with { AccessToken = null, Items = [], Error = action.Message, Pending = false, ActiveRequest = null, SessionVersion = state.SessionVersion + 1 };
        return state with { Error = action.Message, Pending = false, ActiveRequest = null };
    }

    [ReducerMethod] public static BoardState Filter(BoardState state, FilterChanged action)
        => state with { Filter = action.Filter };

    [ReducerMethod] public static BoardState Logout(BoardState state, LogoutRequested action)
        => state with { AccessToken = null, Items = [], Error = null, Filter = "All", Pending = false, ActiveRequest = null, SessionVersion = state.SessionVersion + 1 };
}
