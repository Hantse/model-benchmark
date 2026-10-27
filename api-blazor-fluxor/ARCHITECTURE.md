# Architecture contract

## Four backend projects

```text
Domain                         WorkItem, WorkStatus, AuthenticatedAccount and domain exceptions
Infrastructure -> Domain       IWorkItemRepository, SqliteWorkItemRepository,
                               work-item/authentication services and repositories
Business -> Domain, Infrastructure
                               separate queries/commands and handlers; typed OneOf outcomes
Host -> Domain, Business, Infrastructure
                               MVC controllers, JWT validation, DI and configuration
Frontend                       independent server-side Blazor host; HTTP only
Api.Tests                      real SQLite, injected services, CQRS, controllers and HTTP tests
Frontend.Tests                 reducers, actual Fluxor effects/store, HTTP adapter and bUnit
```

Services and repositories belong to Infrastructure. `WorkItemService` receives
`IWorkItemRepository`; it owns input validation, ownership checks and filtering.
Domain contains no ASP.NET, SQLite, Fluxor or Business dependencies. Business has
no ASP.NET or SQLite calls: it orchestrates Infrastructure through
`IWorkItemService`, separating `ListWorkItems` queries from create/update/delete
commands. Host composes the implementations but its controllers must depend on
`IWorkItemQueries` / `IWorkItemCommands`, not repositories or database connections.

## CQRS and OneOf at the HTTP boundary

`WorkItemQueries.Handle` and the overloaded `WorkItemCommands.Handle` methods
return the provided typed `OneOf` outcomes. Successful query/create/update values
contain domain entities; delete uses `Deleted`. Expected failures use
`ValidationError`, `NotFound` and `Conflict` where applicable. Handlers translate
Infrastructure's documented validation/not-found/version-conflict exceptions
into the correct union case. Unexpected storage or cancellation failures must
not be disguised as a successful result or a domain conflict.

Host controllers consume those real results with exhaustive `OneOf.Match` or `Switch`,
mapping validation to 400, missing/foreign items to 404, stale versions to 409,
and successes to 200/201/204 as specified in `FEATURES.md`. Adding a OneOf package
or declaring an unused union does not satisfy this requirement.

Expose `/health`, `/api/auth/login` and `/api/work-items` through real MVC
`[ApiController]` / `ControllerBase` classes under `Host/Controllers`.
`Host/Program.cs` contains dependency registration, configuration, authentication,
middleware, `MapControllers()` and host startup only. Do not define endpoint
delegates in Program or move minimal endpoints into an extension method.
Controllers parse HTTP DTOs and authentication subjects, dispatch CQRS requests
and map results; they do not implement repository operations or duplicate the
service's business rules. Repository owners never appear in public item DTOs.

Authentication follows the same boundary: Infrastructure's
`IAuthenticationService.AuthenticateAsync` validates the synthetic credentials
and returns an `AuthenticatedAccount`, or null for invalid credentials. Missing
credentials produce `ArgumentException`. Business's `AuthCommands.Handle(LoginCommand)`
returns `OneOf<AuthenticatedAccount, ValidationError, Unauthorized>`.
`AuthController` matches those cases to signed JWT/200, 400 and 401. Host owns JWT
transport configuration/signing; the account/password decisions belong to the
Infrastructure authentication service, not the controller.

## SQLite persistence and concurrency

`SqliteWorkItemRepository(string connectionString)` implements
`IWorkItemRepository`. `InitializeAsync` creates the schema idempotently without
clearing existing data. Host reads `ConnectionStrings:WorkItems` and uses that
same database for all work-item operations. Development configuration uses a local
SQLite file; tests use isolated temporary files and real connections.
No external database server or Entity Framework dependency is required.

Data must survive disposal of a repository, opening new connections and restarting
Host against the same file. Separate configured files must remain isolated.
Use parameterized SQL for every user-controlled value. List results are snapshots
and contain only the requested owner's items.

`ReplaceAsync` atomically succeeds only when id, owner and stored version match;
the replacement version must be exactly `expectedVersion + 1`.
`DeleteAsync` atomically checks id, owner and version. A missing/foreign/stale
write must leave storage unchanged. Concurrent mutations through independent
repository instances/connections must have exactly one winner for a given
version. A C# lock around a process-local dictionary or an unchecked write after
a service-level read is insufficient: enforce predicates in SQL and inspect
affected rows. SQLite transactions and normal database locks are allowed.

Duplicate UUID insertion must raise a storage error without replacing the stored
entity. Read-only or unavailable SQLite storage must report its actual failure;
do not silently switch to a dictionary or report a business conflict.

Honor pre-cancelled cancellation tokens without mutation. No true asynchronous
SQLite I/O is required: Microsoft.Data.Sqlite executes its asynchronous ADO.NET
methods synchronously. See [Microsoft's limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async).

## Real scoped Fluxor in Blazor

The independent Blazor server uses interactive server rendering and
`AddFluxor(...ScanAssemblies(...).WithLifetime(StoreLifetime.Scoped))`.
Each circuit has an isolated store. `Routes.razor` initializes it with
`Fluxor.Blazor.Web.StoreInitializer`. `WorkBoard` inherits `FluxorComponent`,
observes `IState<BoardState>` and dispatches actions through `IDispatcher`.

The flow is **component dispatches action -> pure reducer creates immutable state
-> effect performs HTTP -> effect dispatches a result action -> reducer updates
state -> component renders**. Reducers never perform I/O or mutate prior state.
Effects never write component fields or mutate state directly. The pinned package
is `Fluxor.Blazor.Web` 6.11.0. See [Fluxor's documented state rules](https://github.com/mrpmorris/Fluxor/blob/master/Docs/README.md).

`BoardState` is the source of truth for access token, session version, items,
selected filter, pending request, active request id and visible error. Form
inputs may be local component fields. Private data, pending status and request
coordination must remain in Fluxor. Do not add locks, monitors, semaphores,
mutexes, reader/writer locks, blocking waits or a separate busy/request registry
to components, reducers, effects or frontend helpers.

A request id identifies one operation. A monotonically increasing session version
invalidates old completions after logout, a new login or a 401. Reducers reject
duplicate pending requests; effects check the accepted request and session before
sending HTTP and before dispatching a completion. Logout remains available during
pending work. An old failure, including 401, cannot change a newer session.

Effects receive `IWorkItemApi` and `IState<BoardState>` by dependency injection.
`WorkItemApiClient` uses a typed `HttpClient`. Set bearer authentication on each
request, never on shared default headers. A successful mutation reloads
authoritative items before clearing pending state. There is no automatic mutation
retry. Fluxor can queue actions dispatched inside an effect; do not assume their
reducers have already executed immediately after `Dispatch` returns.

## Public extension points and isolation

Keep the provided public Domain, Infrastructure service/repository, Business
CQRS, Host DTO/controller and Frontend contracts used by the protected tests.
Additional implementation files and private helpers under `src` are allowed.
Pinned projects, packages, locks and tests are protected.

Tests inject recording repositories/services into Infrastructure and Business,
while database and HTTP integration tests use the actual SQLite implementation.
Test hosts use their own database unless deliberately sharing a path to verify
restart persistence. Frontend stores are scoped and have no persistent token
storage, browser local storage, cookie authentication or database connection.
