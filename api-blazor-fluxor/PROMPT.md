# Task: complete the private versioned work board

Implement the three features in `FEATURES.md` while preserving the dependency boundaries in `ARCHITECTURE.md` and the editing rules in `AGENTS.md`.

1. Add configured, signed synthetic JWT login for Alice, Bob and Admin.
2. Implement private work-item CRUD with status filtering and atomic version checks through Domain, Application, Infrastructure and API layers.
3. Complete the server-side Blazor work board using the provided Fluxor state, actions, reducers and effects. Keep login tokens in the scoped circuit store, clear private data on logout or HTTP 401, and reject asynchronous results from an earlier session.

The feature count is three. Deliver working source code within the assigned time budget, then run both validation commands from `benchmark.json`. The starter compiles but intentionally does not implement these behaviors. Use the existing pinned dependencies. Do not change, disable or bypass the protected tests, manifests, configuration or instructions. Do not fabricate reports or replace the runner.

Keep the public signatures used by tests. You may add implementation files and Razor components under `src/`. Favor clear, maintainable code; an API route should delegate business behavior to the application service, and effects should use the HTTP adapter rather than directly accessing a repository.

Acceptance: every one of the 82 discovered tests passes, with no skipped tests. The public tests define a reproducible minimum; implement the complete documented contract even where a particular case is not asserted directly.
