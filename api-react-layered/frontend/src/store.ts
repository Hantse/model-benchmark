import { configureStore, createAction, createSlice, type ThunkAction, type UnknownAction } from '@reduxjs/toolkit';

export type WorkStatus = 'Todo' | 'InProgress' | 'Done';
export type Filter = 'all' | WorkStatus;
export type WorkItem = { id: string; title: string; status: WorkStatus; version: number };
export type AuthState = { token: string | null; email: string | null; pending: boolean; error: string | null; generation: number };
export type BoardState = { items: WorkItem[]; filter: Filter; pending: boolean; error: string | null };
export const loggedOut = createAction('session/loggedOut');
export const filterChanged = createAction<Filter>('board/filterChanged');
const authSlice = createSlice({ name: 'auth', initialState: { token: null, email: null, pending: false, error: null, generation: 0 } as AuthState, reducers: {} });
const boardSlice = createSlice({ name: 'board', initialState: { items: [], filter: 'all', pending: false, error: null } as BoardState, reducers: {} });
export const authReducer = authSlice.reducer;
export const boardReducer = boardSlice.reducer;
export const makeStore = () => configureStore({ reducer: { auth: authReducer, board: boardReducer } });
export type AppStore = ReturnType<typeof makeStore>;
export type RootState = ReturnType<AppStore['getState']>;
export type AppDispatch = AppStore['dispatch'];
type AppThunk = ThunkAction<Promise<void>, RootState, unknown, UnknownAction>;
// Implement async effects and reducers; these compiling no-op thunks are intentional.
export const login = (_input: { email: string; password: string }): AppThunk => async () => {};
export const loadItems = (): AppThunk => async () => {};
export const createItem = (_input: { title: string; status: WorkStatus }): AppThunk => async () => {};
export const updateItem = (_input: { id: string; title: string; status: WorkStatus; expectedVersion: number }): AppThunk => async () => {};
export const deleteItem = (_input: { id: string; expectedVersion: number }): AppThunk => async () => {};
export const selectVisibleItems = (state: RootState): WorkItem[] => state.board.items;
