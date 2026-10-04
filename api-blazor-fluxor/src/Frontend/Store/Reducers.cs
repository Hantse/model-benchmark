using Fluxor;
namespace Benchmark.Frontend.Store;
// Reducers intentionally do nothing. State transitions are part of the task.
public static class Reducers
{
    [ReducerMethod] public static BoardState Login(BoardState state, LoginRequested action) => state;
    [ReducerMethod] public static BoardState LoginSuccess(BoardState state, LoginSucceeded action) => state;
    [ReducerMethod] public static BoardState Load(BoardState state, LoadRequested action) => state;
    [ReducerMethod] public static BoardState Add(BoardState state, AddRequested action) => state;
    [ReducerMethod] public static BoardState Update(BoardState state, UpdateRequested action) => state;
    [ReducerMethod] public static BoardState Delete(BoardState state, DeleteRequested action) => state;
    [ReducerMethod] public static BoardState Loaded(BoardState state, ItemsLoaded action) => state;
    [ReducerMethod] public static BoardState Failed(BoardState state, OperationFailed action) => state;
    [ReducerMethod] public static BoardState Filter(BoardState state, FilterChanged action) => state;
    [ReducerMethod] public static BoardState Logout(BoardState state, LogoutRequested action) => state;
}
