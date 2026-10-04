# Architecture contract

## Projects and dependency direction

```text
Domain                 WorkItem and WorkStatus; no project dependencies
Application -> Domain  IWorkItemRepository + WorkItemService; no web or storage dependencies
Infrastructure -> Application, Domain
                       thread-safe in-memory repository; atomic compare-and-swap
Api -> Application, Infrastructure
                       HTTP routing, configured JWT validation and dependency composition
Frontend               independent server-side Blazor host; HTTP only, no project references
Api.Tests              HTTP integration and injected fake-repository/layer tests
Frontend.Tests         reducers, actual Fluxor effects/store, HTTP adapter and bUnit components
```

Do not put repository state or ownership logic in API handlers. `WorkItemService` owns validation, ownership checks and filtering and receives `IWorkItemRepository` in its constructor. Infrastructure implements the interface, including atomic version checks. Repository entities contain the owner; HTTP DTOs do not expose it.

`ReplaceAsync` atomically succeeds only when the stored version matches `expectedVersion`; ownership and identity must also remain valid. `DeleteAsync` atomically checks id, owner and version. Two concurrent mutations based on the same version must have exactly one winner. A service-level check followed by an unguarded write is insufficient.

## Frontend and Fluxor

The independent Blazor server uses interactive server rendering and `AddFluxor(...ScanAssemblies(...).WithLifetime(StoreLifetime.Scoped))`. Each circuit has an isolated store. `Routes.razor` initializes the store with `Fluxor.Blazor.Web.StoreInitializer`. `WorkBoard` inherits `FluxorComponent`, observes `IState<BoardState>` and dispatches actions through `IDispatcher`.

This follows the real Fluxor [state/actions/reducers tutorial](https://github.com/mrpmorris/Fluxor/blob/master/Source/Tutorials/02-Blazor/02A-StateActionsReducersTutorial/README.md) and [effects tutorial](https://github.com/mrpmorris/Fluxor/blob/master/Source/Tutorials/02-Blazor/02B-EffectsTutorial/README.md). The pinned package is `Fluxor.Blazor.Web` 6.11.0; use the APIs in that version.

`BoardState` is the immutable source of truth for access token, session version, items, selected filter, pending request, active request id and visible error. Form inputs may be local component fields; private data and asynchronous operation state must use Fluxor. A request id identifies one operation. A monotonically increasing session version invalidates old completions after logout, a new login or a 401.

Effects receive `IWorkItemApi` and `IState<BoardState>` by dependency injection. `WorkItemApiClient` is the production adapter using a typed `HttpClient`. Bearer authentication is set on each request, never on shared default headers. Effects capture their token/session/request and verify relevance before publishing results. No automatic mutation retry. A successful mutation reloads authoritative items before clearing pending state.

A login success may enqueue a `LoadRequested` action; account for Fluxor dispatch ordering when an effect dispatches further actions. Do not assume queued reducers have already executed immediately inside the same effect.

## Public extension points

Keep the existing signatures in `Domain/WorkItem.cs`, `Application/Contracts.cs`, `Application/WorkItemService.cs`, `Api/Contracts.cs`, `Frontend/Api/Contracts.cs`, `Frontend/Store/BoardState.cs`, `Actions.cs` and `Reducers.cs`. The tests construct these types and inject fake dependencies. Additional private helpers and source files are allowed.

The API uses a singleton in-memory repository per host and a scoped application service. Tests create isolated hosts/repositories/stores. The frontend uses a scoped circuit store and has no persistent token storage, browser local storage, cookie authentication or database.
