# Features and acceptance contract

Exactly three features are required.

## 1. Synthetic JWT authentication

`POST /api/auth/login` accepts JSON `{ "email": "alice@example.test", "password": "Alice!234" }` and returns HTTP 200 with `{ "accessToken": "<signed JWT>" }`.

| Email | Password | Role |
| --- | --- | --- |
| alice@example.test | Alice!234 | User |
| bob@example.test | Bob!234 | User |
| admin@example.test | Admin!234 | Admin |

These are synthetic public test accounts. Empty/missing credentials return 400; unknown accounts or wrong passwords return 401. Use configured `Jwt:SigningKey`, `Jwt:Issuer`, `Jwt:Audience`, HS256, an expiry 15 minutes after issuance, and a `sub` claim equal to the email. Include the account's role claim. Validate signature, issuer, audience and lifetime; reject malformed/expired/wrongly signed tokens with 401. Read the required JWT configuration rather than hardcoding signing material; do not silently fall back to a default key.

`GET /health` remains public and returns 200. Unknown routes return 404. All work-item endpoints require a valid authenticated subject. Admin's work items are still private; the Admin role does not grant another user's data or bypass ownership checks.

## 2. Private versioned work items

A returned item is `{ "id": "<UUID>", "title": "Plan", "status": "Todo", "version": 1 }`. The repository's owner is derived exclusively from the authenticated JWT subject and is never returned or accepted in a public DTO.

| Method and path | Input | Successful response |
| --- | --- | --- |
| GET `/api/work-items` | optional `?status=Todo`, `InProgress` or `Done` | 200, array of own matching items |
| POST `/api/work-items` | `{ "title": "Plan", "status": "Todo" }` (`status` defaults to Todo) | 201, item and `Location: /api/work-items/{id}` |
| PUT `/api/work-items/{id}` | `{ "title": "New", "status": "Done", "expectedVersion": 1 }` | 200, updated item with version 2 |
| DELETE `/api/work-items/{id}` | required query `?expectedVersion=1` | 204, no response body |

Trim titles; their trimmed length must be 1 through 120 characters. Accepted status strings are exactly `Todo`, `InProgress`, `Done`. New items have a non-empty UUID and version 1. Reject invalid titles/statuses/filters and missing or non-positive expected versions with 400 without mutation. List order is not specified; tests that need ordering use only one matching item.

Updates increase version by exactly one. A stale expected version returns 409 and leaves the latest entity unchanged; stale deletion also returns 409 without removing the item. Unknown or foreign ids return 404 **before any version comparison**, including a request carrying another owner's stale version. A successful delete followed by another delete returns 404. Simultaneous writes using the same expected version must have exactly one winner; the others return 409.

Implement this through the application service and the injected repository interface. Layer tests exercise validation and isolation through a fake repository; infrastructure tests independently exercise atomic compare-and-swap. Do not duplicate these rules only in API routes.

## 3. Blazor UI with a scoped Fluxor store

Keep the public `Work Board` heading. Provide an accessible email/password login, authenticated item creation and editing, deletion, status filtering, logout and an error message with `role="alert"`. Display title, status and version for each item. A row edits its title/status and submits the currently displayed server version. Use native inputs/selects/buttons; the tests locate the stable hooks below.

| Hook (`data-testid`) | Meaning |
| --- | --- |
| email, password, login | login inputs and submit button; password input has type password |
| title, create-status, add | creation inputs and submit; status selector contains all three statuses |
| filter | selector containing All, Todo, InProgress, Done |
| items, item-row | list container and one visible row per matching item |
| edit-title, edit-status, save, delete | per-row edit inputs and mutation buttons |
| logout | logout button |

The anonymous view exposes login and no work-item editor. After login, load items using the returned bearer token. Filtering updates Fluxor state and the visible subset without destroying the unfiltered array. `All` restores every loaded item.

`BoardState` defaults to anonymous, empty items, filter All, no pending request and no error. `LoginRequested` starts a new session version, clears previous token/items/error, and becomes pending. Matching `LoginSucceeded` sets the token and finishes that request; the effect then loads authoritative items. Load/add/update/delete requests require authentication and become pending while preserving previously loaded items. Ignore duplicate requests while pending; disable mutation/login submit buttons until the operation finishes. Logout remains available to invalidate pending work.

A matching `ItemsLoaded` replaces items and clears pending/error. A matching non-401 `OperationFailed` shows the error, clears pending, keeps existing token/items and performs no automatic retry. A matching 401 clears token/private items and invalidates the session. Logout clears token, items, error, filter and pending state and increments the session version. Both reducers and effects must reject stale request/session results, including a login or read completing after logout. A late old 401 must not log out a newer session.

Use the provided real Fluxor actions/reducers/effects. `IWorkItemApi` is injected so tests can control asynchronous completion. Tokens remain only in the scoped store's memory for the Blazor circuit; do not persist them or use a singleton store. The HTTP adapter uses the exact API routes and versions and maps unsuccessful responses to `ApiFailureException` with their HTTP status.

