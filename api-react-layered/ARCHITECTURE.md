# Architecture and fixed integration contracts

## API dependency direction

```text
WorkBoard.Domain
       ^
WorkBoard.Application  <--- WorkBoard.Infrastructure
       ^                          ^
       +----- WorkBoard.Api -------+
```

Domain contains WorkItem and WorkStatus. It must not depend on the application,
infrastructure, API, ASP.NET Core or a database framework. Application depends on
Domain and contains IWorkItemRepository and WorkItemService. It must not depend on
Infrastructure, API or ASP.NET Core. Infrastructure implements the application
repository interface and must not reference API. API is the composition root,
configures authentication and translates HTTP requests/results; business rules
and storage do not belong in route handlers.

The supplied project references are protected. Preserve the public types and
method signatures; implementation helpers may be added under src/.
Register IWorkItemRepository as an instance-local singleton backed by
InMemoryWorkItemRepository, and WorkItemService through DI. Its public Repository
property must identify the repository injected into its constructor. Routes must
use WorkItemService rather than instantiate or bypass a repository.

IWorkItemRepository exposes owner-scoped List/Find/Add and atomic TryUpdate and
TryDelete. MutationResult reports Success, NotFound or Conflict. A successful
TryUpdate returns the updated immutable WorkItem. WorkItemService returns
ServiceResult/ListResult with the HTTP-equivalent codes specified in FEATURES.md,
validates title/status/version, and maps repository outcomes. Repository List
returns a safe snapshot, not a live mutable collection; compare-and-swap updates
must be atomic across concurrent requests.

## Redux integration

Use the pinned @reduxjs/toolkit and react-redux packages. `main.tsx` provides a
store to App through `<Provider>`. Tests also render App with their own Provider.
Do not create a hidden global store inside App.

The public exports in `frontend/src/store.ts` are:

- `makeStore()`, `authReducer`, `boardReducer`, RootState, AppStore and AppDispatch.
- `loggedOut()`, `filterChanged(filter)`.
- Async thunks `login({ email, password })`, `loadItems()`,
  `createItem({ title, status })`,
  `updateItem({ id, title, status, expectedVersion })`,
  `deleteItem({ id, expectedVersion })`.
- `selectVisibleItems(state)` and the WorkItem, WorkStatus and Filter types.

The root state contains `auth` with token/email/pending/error/generation, and
`board` with items/filter/pending/error. Preserve these fields. Additional internal
state is allowed. Token and email initially are null, arrays empty, filter `all`,
pending false and error null. Async thunks resolve after their result is reduced;
HTTP errors are represented in state rather than an unhandled rejection.

Reducers must not perform HTTP calls or side effects. Thunks/effects dispatch
actions and capture session generation; reducers or guarded effects reject old
completions. Components consume Redux selectors/state and dispatch the public
actions/thunks. Redux DevTools or persistence must not serialize a token to
browser storage. UI requests use same-origin `/api` URLs; Vite proxies them to
the API on http://127.0.0.1:5080 during local development.

Official documentation: [Redux Toolkit](https://redux.js.org/toolkit/introduction/getting-started)
and [React Redux](https://redux.js.org/react-redux/introduction/getting-started).
