# Task: complete the private versioned work board

Implement the three features in `FEATURES.md` while preserving the dependency boundaries in `ARCHITECTURE.md` and the editing rules in `AGENTS.md`.

The required backend structure is **Domain / Infrastructure (services and repositories) / Business (CQRS) / Host (MVC controllers)**. These are separate projects with real responsibilities, not merely folder names.

1. Add configured, signed synthetic JWT login for Alice, Bob and Admin through an Infrastructure authentication service, a Business CQRS login handler returning OneOf outcomes and a Host controller.
2. Implement private work-item CRUD with status filtering and atomic version checks against a real, configurable, file-backed SQLite database. Infrastructure contains the service and repository; Business contains separate query/command handlers returning typed `OneOf` results. Host controllers exhaustively match those results to HTTP responses.
3. Complete the server-side Blazor work board using the provided real Fluxor state, actions, reducers and effects. Keep login tokens in the scoped circuit store, clear private data on logout or HTTP 401, and reject asynchronous results from an earlier session. Coordinate pending requests through Fluxor state and request/session identities, without locks, semaphores, blocking waits or a second state store.

The feature count is three. Deliver working source code within the assigned time budget, then run both validation commands from `benchmark.json`. The starter compiles but intentionally does not implement these behaviors. Use the existing pinned dependencies. Do not change, disable or bypass the protected tests, manifests, configuration or instructions. Do not fabricate reports or replace the runner.

Keep the public signatures used by tests. You may add implementation files and Razor components under `src/`. Expose every API endpoint, including authentication and health, through MVC controllers in `src/Host/Controllers/`. `Host/Program.cs` only composes dependencies, configuration and middleware, and calls `MapControllers()`; do not implement endpoint delegates there or in extension methods. Controllers delegate operations to Business CQRS handlers and use exhaustive `OneOf.Match` or `Switch` to map every result case. Effects use the HTTP adapter rather than directly accessing a service or repository.

Acceptance: every test required by `benchmark.json` passes, with no skipped tests. This includes real SQLite persistence/concurrency tests, CQRS and controller tests, HTTP integration tests, and actual Fluxor/bUnit tests. The public tests define a reproducible minimum; implement the complete documented contract even where a particular case is not asserted directly.
