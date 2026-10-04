# Three features to implement

## 1. Local session per Blazor circuit

Implement ITaskBoardService/TaskBoardService with the synthetic accounts
alice@example.test / Alice!234 and bob@example.test / Bob!234. LoginAsync returns
true only for the exact credentials and exposes CurrentUser; every failed login
clears the previous identity. Logout removes the identity. Calling List/Add/
SetCompleted/Delete without an identity throws UnauthorizedAccessException.
Use a scoped service, with no static identity or browser storage. This tester does not use JWTs or an API.

## 2. Private tasks and filtering

Isolate in-memory tasks by email within the service; users must recover their tasks
after logout/login within the same service instance. AddAsync trims the title,
requires 1..120 characters, throws ArgumentException otherwise without mutation,
assigns a new Guid and sets Completed=false.
ListAsync with All/Active/Completed returns all/false/true respectively without
discarding the other tasks. SetCompletedAsync preserves id/title and returns the
persisted state. DeleteAsync deletes only a task belonging to the current account.
An ID that is absent or belongs to another account throws KeyNotFoundException,
without mutation. Preserve the public interfaces.

## 3. Accessible interactive component

TaskBoardPage preserves the h1 `Task Board`. Include an Email field, a Password field
with type=password, and a Sign in button. After login, show the list, a Task title
field, Add task, a checkbox and Delete button per task, a Filter select with values
`All`, `Active`, `Completed`, and a Sign out button.
The component must also work when the service is already logged in before rendering.
Errors use role=alert. Login/logout update the view; logout removes all private data
from the view. Successful operations refresh the filtered list.
Keep these test hooks (data-testid attribute):

| Hook | Element |
|---|---|
| email / password / login | Login fields and button |
| task-title / add | Task creation |
| tasks | List container, present even when empty while logged in |
| task-row / complete / delete | Row, checkbox and button per task |
| filter / logout | Select and button |

No required CSS/framework, database, external service, registration or deployment.
