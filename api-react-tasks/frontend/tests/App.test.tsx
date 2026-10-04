import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import App from '../src/App';

type Task = { id: string; title: string; completed: boolean };
let tasks: Task[];
let failingMutation: boolean;
let expired: boolean;
const requests: { path: string; method: string; bearer: string | null; body: unknown }[] = [];
beforeEach(() => {
  tasks = []; failingMutation = false; expired = false; requests.length = 0;
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = typeof input === 'string' ? input : input.toString();
    const method = init?.method ?? 'GET';
    const headers = new Headers(init?.headers);
    const body = init?.body ? JSON.parse(String(init.body)) : null;
    requests.push({ path, method, bearer: headers.get('Authorization'), body });
    if (path === '/api/auth/login') return body?.email === 'alice@example.test' && body?.password === 'Alice!234'
      ? Response.json({ accessToken: 'signed-test-token' }) : Response.json({ error: 'Invalid credentials' }, { status: 401 });
    if (headers.get('Authorization') !== 'Bearer signed-test-token' || expired) return Response.json({ error: 'Unauthorized' }, { status: 401 });
    if (method === 'GET') return Response.json(tasks);
    if (failingMutation) return Response.json({ error: 'Could not save task' }, { status: 500 });
    if (method === 'POST') { const task = { id: `task-${tasks.length + 1}`, title: body.title.trim(), completed: false }; tasks.push(task); return Response.json(task, { status: 201 }); }
    const id = path.split('/').pop();
    if (method === 'PUT') { const index = tasks.findIndex(t => t.id === id); tasks[index] = { ...tasks[index], ...body }; return Response.json(tasks[index]); }
    if (method === 'DELETE') { tasks = tasks.filter(t => t.id !== id); return new Response(null, { status: 204 }); }
    return Response.json({}, { status: 404 });
  }));
});
afterEach(() => vi.unstubAllGlobals());
async function login() {
  const user = userEvent.setup();
  await user.type(screen.getByLabelText('Email'), 'alice@example.test');
  await user.type(screen.getByLabelText('Password'), 'Alice!234');
  await user.click(screen.getByRole('button', { name: 'Sign in' }));
  await screen.findByRole('button', { name: 'Sign out' });
  return user;
}
async function add(user: ReturnType<typeof userEvent.setup>, title = 'Build app') { await user.type(screen.getByLabelText('Task title'), title); await user.click(screen.getByRole('button', { name: 'Add task' })); await screen.findByText(title); }

describe('Task board acceptance', () => {
  it('preserves public heading', () => { render(<App />); expect(screen.getByRole('heading', { name: 'Task Board' })).toBeTruthy(); });
  it('shows accessible login and hides private form initially', () => { render(<App />); expect(screen.getByLabelText('Email')).toBeTruthy(); expect(screen.getByLabelText('Password').getAttribute('type')).toBe('password'); expect(screen.queryByLabelText('Task title')).toBeNull(); expect(requests).toEqual([]); });
  it('rejects invalid login visibly', async () => { render(<App />); const u = userEvent.setup(); await u.type(screen.getByLabelText('Email'), 'alice@example.test'); await u.type(screen.getByLabelText('Password'), 'wrong'); await u.click(screen.getByRole('button', { name: 'Sign in' })); expect((await screen.findByRole('alert')).textContent).toBeTruthy(); expect(screen.queryByLabelText('Task title')).toBeNull(); });
  it('sends credentials and uses returned bearer for task reads', async () => { render(<App />); await login(); expect(requests[0]).toMatchObject({ path: '/api/auth/login', method: 'POST', body: { email: 'alice@example.test', password: 'Alice!234' } }); await waitFor(() => expect(requests.some(r => r.path === '/api/tasks' && r.bearer === 'Bearer signed-test-token')).toBe(true)); });
  it('adds through authenticated API and renders response', async () => { render(<App />); const u = await login(); await add(u); expect(tasks).toHaveLength(1); expect(requests.find(r => r.path === '/api/tasks' && r.method === 'POST')).toMatchObject({ bearer: 'Bearer signed-test-token', body: { title: 'Build app' } }); });
  it('completes through PUT then filters without losing data', async () => { render(<App />); const u = await login(); await add(u, 'Pending'); await u.click(screen.getByRole('checkbox', { name: 'Complete Pending' })); await waitFor(() => expect(tasks[0].completed).toBe(true)); expect(requests.find(r => r.method === 'PUT')).toMatchObject({ path: '/api/tasks/task-1', bearer: 'Bearer signed-test-token', body: { title: 'Pending', completed: true } }); await u.selectOptions(screen.getByLabelText('Filter'), 'active'); expect(screen.queryByText('Pending')).toBeNull(); await u.selectOptions(screen.getByLabelText('Filter'), 'completed'); expect(screen.getByText('Pending')).toBeTruthy(); await u.selectOptions(screen.getByLabelText('Filter'), 'all'); expect(screen.getByText('Pending')).toBeTruthy(); });
  it('deletes through authenticated API', async () => { render(<App />); const u = await login(); await add(u); await u.click(screen.getByRole('button', { name: 'Delete Build app' })); await waitFor(() => expect(screen.queryByText('Build app')).toBeNull()); expect(tasks).toHaveLength(0); expect(requests.find(r => r.method === 'DELETE')).toMatchObject({ path: '/api/tasks/task-1', bearer: 'Bearer signed-test-token' }); });
  it('failed create stays visible as error and does not invent success', async () => { render(<App />); const u = await login(); failingMutation = true; await u.type(screen.getByLabelText('Task title'), 'Never saved'); await u.click(screen.getByRole('button', { name: 'Add task' })); expect((await screen.findByRole('alert')).textContent).toBeTruthy(); expect(screen.queryByText('Never saved')).toBeNull(); expect(tasks).toHaveLength(0); });
  it('logout removes private data and token', async () => { render(<App />); const u = await login(); await add(u, 'Secret'); await u.click(screen.getByRole('button', { name: 'Sign out' })); expect(screen.queryByText('Secret')).toBeNull(); expect(screen.getByRole('button', { name: 'Sign in' })).toBeTruthy(); expect(localStorage.length).toBe(0); expect(sessionStorage.length).toBe(0); });
  it('expired task session returns to login and clears data', async () => { render(<App />); const u = await login(); await add(u, 'Private'); expired = true; await u.click(screen.getByRole('button', { name: 'Delete Private' })); await screen.findByRole('button', { name: 'Sign in' }); expect(screen.queryByText('Private')).toBeNull(); });
});
