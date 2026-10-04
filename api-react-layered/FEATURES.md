# Three required features

## 1. JWT identity and private ownership

Implement `POST /api/auth/login` with JSON `{ "email": "...", "password": "..." }`.
The fixed synthetic accounts are:

| Email | Password | Role |
|---|---|---|
| alice@example.test | Alice!234 | User |
| bob@example.test | Bob!234 | User |
| admin@example.test | Admin!234 | Admin |

Blank email or password returns 400; unknown accounts or wrong passwords return
401. Success returns 200 and `{ "accessToken": "JWT" }`. Use HS256 with `sub` equal
to the email, a signed `role` claim, and a lifetime of 15 minutes. Read
`Jwt:SigningKey`, `Jwt:Issuer` and `Jwt:Audience` from .NET configuration. Validate
signature, issuer, audience and expiration on every work-item endpoint. Tests
provide a synthetic signing key. Do not hardcode a production key.

Every work-item endpoint requires a valid bearer token and returns 401 before any
unauthenticated mutation. Ownership always comes from the validated JWT, never
from request JSON. An administrator has the same private collection rules as any
other user. Preserve public `GET /health` (200, `{ "status": "ok" }`) and unknown
routes (404). Registration, refresh tokens and external identity services are out
of scope.

## 2. Layered versioned work items

Implement the application service and the injected thread-safe in-memory
repository described in ARCHITECTURE.md. Storage starts empty and is separate
for each server instance and owner. Public JSON DTO:
`{ "id": "GUID", "title": "Release", "status": "Todo", "version": 1 }`.
Do not expose the owner in the DTO.

| Endpoint | Contract |
|---|---|
| `GET /api/work-items` | 200, array of the current owner's items |
| `GET /api/work-items?status=Done` | Exact status filter; absent means all |
| `POST /api/work-items` | `{ "title": "...", "status": "Todo" }`; 201, DTO and Location `/api/work-items/{id}` |
| `PUT /api/work-items/{id}` | `{ "title": "...", "status": "Done", "expectedVersion": 1 }`; 200 and updated DTO |
| `DELETE /api/work-items/{id}?expectedVersion=1` | 204, no content |

Trim titles and accept 1 through 120 characters inclusive. Allowed status strings
are exactly `Todo`, `InProgress`, `Done`; invalid values or filters return 400.
Omitted POST status defaults to `Todo`. New IDs are unique nonempty GUIDs and
versions start at 1. A successful update increments the version exactly once.
PUT and DELETE require a positive expectedVersion; malformed or missing values
return 400. Invalid input must leave both data and version unchanged.

A valid request for a missing item or another owner's item returns 404 without
disclosure or mutation. A stale version for an owned existing item returns 409
without mutation. Compare version and mutate atomically: simultaneous updates
with the same current version admit exactly one success. DELETE also compares
the version atomically. Map application results to the specified HTTP statuses.
The API must remain usable when tests replace IWorkItemRepository in DI.

## 3. React workflow through Redux Toolkit

Complete `frontend/src/store.ts` and the React UI. Keep the exported store factory,
types, reducers, actions, thunks and selector signatures in ARCHITECTURE.md.
Session, work items, filter, pending flags and errors belong to Redux. Components
read the store through react-redux and dispatch its actions/effects. Local state
is allowed for unfinished form input, not a second private item collection.

The login screen has labels `Email`, `Password` and button `Sign in`. Send the
real HTTP login and keep the returned token only in memory. On successful login,
load `/api/work-items` with `Authorization: Bearer <token>`. Never persist tokens
or private items in localStorage, sessionStorage, cookies or a persistence plugin.

The signed-in screen lists item titles and includes:

- Field `Work item title`, select `New item status` with the three status values,
  and button `Add item`.
- Select `Filter`, with values `all`, `Todo`, `InProgress`, `Done`.
- Per-row select labelled `Status <title>` and button `Delete <title>`.
- Button `Sign out`. Preserve the public heading `Work Board`.

POST creates; PUT changes a row's status while retaining its title and sending
its current version; DELETE sends its current expectedVersion in the query.
Use server responses as the source of successful changes. Filtering only changes
visible rows and keeps the complete stored collection. Show failures in an
element with `role="alert"`; never invent success on 409 or 500. Pending flags
become true before awaiting HTTP and false after completion. Disable relevant
submit controls while pending.

Logout clears identity, items, filter and pending operations synchronously. A
401 from a current private request does the same and returns to login. Capture a
session generation or equivalent guard for every async effect: successful login,
load or mutation completions after logout must not restore identity/private
data, and an old request's 401 must not invalidate a newer session. Redux state
changes must be immutable; separate makeStore instances must be isolated.
