# Contract for the three features

## 1. JWT authentication

Implement `POST /api/auth/login` with JSON `{ "email": "...", "password": "..." }`.
Fixed synthetic accounts: `alice@example.test` / `Alice!234`, `bob@example.test` /
`Bob!234`, `admin@example.test` / `Admin!234`. Their respective roles are `User`, `User`, `Admin`.
These are not real accounts. Empty or whitespace-only email/password: 400;
unknown or incorrect credentials: 401; success: 200 and `{ "accessToken": "JWT" }`.
Use HS256 JWTs, `sub` = email, a signed role claim and a 15-minute expiry.
Read the issuer, audience and signing key from `Jwt:Issuer`, `Jwt:Audience`
and `Jwt:SigningKey` (.NET configuration).
Do not include production keys in the sources. Fully validate the signature,
issuer, audience and expiry; never accept a token that expired ten minutes ago.
The tests provide a synthetic key through configuration. For a manual run,
set `Jwt__SigningKey` (at least 64 characters), `Jwt__Issuer=benchmark-task-api`
and `Jwt__Audience=benchmark-task-client` in the process environment.

## 2. Private tasks

Every route below requires a valid bearer JWT; otherwise return 401 before any mutation.
Use initially empty, thread-safe, in-memory storage per server instance, partitioned by `sub`.
Always derive ownership from the JWT. Public DTO: `{ "id": "GUID", "title": "text", "completed": false }`.

| Route | Contract |
|---|---|
| `GET /api/tasks` | 200, an array containing only the current user's tasks; an empty array is allowed |
| `GET /api/tasks?completed=true` or `false` | Exact filter on completed; if omitted, return all tasks |
| `POST /api/tasks` | `{ "title": "...", "completed": false }`; 201, the DTO and Location `/api/tasks/{id}` |
| `PUT /api/tasks/{id}` | Same input, replace title/completed; 200 and the DTO |
| `DELETE /api/tasks/{id}` | 204 with no content |

Trim the title, then require 1 to 120 characters inclusive; otherwise return 400 without mutation.
Assign a new, non-empty, unique ID on creation. An ID that is absent or belongs to another
account must return 404 on PUT/DELETE, without disclosure or mutation. An admin must not
see any additional tasks through `/api/tasks`. No database, registration, refresh tokens,
deployment or external services.
Preserve public `GET /health` (200, `{ "status": "ok" }`) and unknown routes (404).

## 3. React interface

Develop `frontend/src/App.tsx`: a login screen with labels `Email` and `Password`,
and a `Sign in` button. Call `/api/auth/login`, keep the token only in memory,
and use `Authorization: Bearer <token>` for every task route.
Display errors in an element with `role="alert"`. Do not implement a fake local login.
After login, show the task titles, a `Task title` field, an `Add task` button,
a `Filter` select with values `all`, `active`, `completed`, a checkbox per row
labelled `Complete <title>`, a button per row labelled `Delete <title>`,
and a `Sign out` button.
Create tasks through POST, complete them through PUT with title/completed, and delete them through DELETE.
Filter without losing the original tasks; `all` must show them again. Update the screen
only when the operation succeeds. A 401 response from any task route must remove the token
and tasks and return to the login screen. Logout must do the same; no local storage
or cookies are required. Preserve the public heading `Task Board`. App.tsx may import
other modules under `frontend/src`. The Vite server proxies `/api` to port 5080.
