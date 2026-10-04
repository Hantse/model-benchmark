import { vi } from 'vitest';
import type { WorkItem } from '../src/store';

export type RequestRecord = { path: string; method: string; bearer: string | null; body: unknown };
export const api = {
  items: [] as WorkItem[], requests: [] as RequestRecord[], failMutation: false, expired: false,
  nextRead: null as null | Promise<Response>, nextLogin: null as null | Promise<Response>,
};
export function installApi() {
  api.items = []; api.requests = []; api.failMutation = false; api.expired = false; api.nextRead = null; api.nextLogin = null;
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = typeof input === 'string' ? input : input.toString();
    const url = new URL(path, 'http://benchmark.test'); const method = init?.method ?? 'GET';
    const bearer = new Headers(init?.headers).get('Authorization'); const body = init?.body ? JSON.parse(String(init.body)) : null;
    api.requests.push({ path, method, bearer, body });
    if (url.pathname === '/api/auth/login') {
      if (api.nextLogin) { const result = api.nextLogin; api.nextLogin = null; return result; }
      return body?.email === 'alice@example.test' && body?.password === 'Alice!234'
        ? Response.json({ accessToken: 'synthetic-signed-token' }) : Response.json({ error: 'Invalid credentials' }, { status: 401 });
    }
    if (bearer !== 'Bearer synthetic-signed-token' || api.expired) return Response.json({ error: 'Unauthorized' }, { status: 401 });
    if (method === 'GET') { if (api.nextRead) { const result = api.nextRead; api.nextRead = null; return result; } return Response.json(api.items); }
    if (api.failMutation) return Response.json({ error: 'Save failed' }, { status: 500 });
    if (method === 'POST') { const item: WorkItem = { id: `item-${api.items.length + 1}`, title: body.title.trim(), status: body.status, version: 1 }; api.items.push(item); return Response.json(item, { status: 201 }); }
    const id = url.pathname.split('/').pop()!; const existing = api.items.find(x => x.id === id);
    if (!existing) return Response.json({ error: 'Missing item' }, { status: 404 });
    const expected = method === 'DELETE' ? Number(url.searchParams.get('expectedVersion')) : body.expectedVersion;
    if (expected !== existing.version) return Response.json({ error: 'Version conflict' }, { status: 409 });
    if (method === 'PUT') { const updated = { ...existing, title: body.title.trim(), status: body.status, version: existing.version + 1 }; api.items = api.items.map(x => x.id === id ? updated : x); return Response.json(updated); }
    if (method === 'DELETE') { api.items = api.items.filter(x => x.id !== id); return new Response(null, { status: 204 }); }
    return Response.json({}, { status: 404 });
  }));
}
export const oneItem: WorkItem = { id: 'private-1', title: 'Private release', status: 'Todo', version: 1 };
export function holdRead() {
  let resolve!: (response: Response) => void;
  api.nextRead = new Promise<Response>(r => { resolve = r; });
  return resolve;
}
export function holdLogin() {
  let resolve!: (response: Response) => void;
  api.nextLogin = new Promise<Response>(r => { resolve = r; });
  return resolve;
}
