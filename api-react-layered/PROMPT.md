# Build the layered Work Board

Complete this deliberately unfinished API and React application. Implement the
three features specified in FEATURES.md and preserve the dependency boundaries
and public contracts in ARCHITECTURE.md. The API must use its injected application
service and repository; the React UI must use the Redux Toolkit store for session,
items, filtering, pending operations and errors.

The hard parts are atomic optimistic concurrency, private ownership, real async
Redux effects, and preventing old requests from restoring data after logout.
Keep the application self-contained. All accounts and keys used by the tests are
synthetic. No external database or service is required.

Modify only editable paths in benchmark.json. Do not edit, remove, skip or replace
tests, manifests, instructions, project references, public exports or validation
commands. You may add implementation modules under the editable source folders.
Run the documented validation commands and correct failures within your budget.
Finish with the files changed, commands actually run and any remaining failures.
