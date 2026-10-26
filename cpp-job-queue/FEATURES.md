# Required behavior

Implement the existing single-threaded public API. No additional public hooks are
required. State belongs to each JobQueue instance and is destroyed with it.
Invalid operations return `false` or an empty optional as specified and must not
change any existing job, capacity count or ordering.

## 1. Validated bounded priority admission

- Construction requires capacity > 0 and max_attempts > 0; otherwise throw
  `std::invalid_argument`.
- `submit(id, payload, priority)` accepts an ID of 1..64 bytes containing only
  ASCII `A-Z`, `a-z`, `0-9`, `_` and `-`. IDs are case-sensitive.
- Payload is 1..256 bytes and contains no NUL byte. Whitespace is allowed and
  preserved verbatim; lengths are byte lengths. Priority is inclusive -100..100.
- Every accepted job starts Queued with attempts zero. Return true on admission.
  Invalid values, duplicate IDs and a full queue return false without mutation.
- Capacity counts only Queued and Running jobs. Terminal jobs are retained, stop
  consuming capacity and reserve their IDs permanently for that queue's lifetime.
- `start_next()` selects a Queued job with highest numeric priority. Ties use
  original successful submission order, independent of lexical ID order. It
  changes that job to Running, increments attempts once and returns a value copy
  of its new state. If no job is Queued, return `std::nullopt`.

## 2. Lifecycle and bounded retries

- `complete(id, true)` only accepts a Running job. It becomes Succeeded, keeps
  its attempt count, releases capacity and returns true.
- `complete(id, false)` only accepts a Running job. If its attempts are strictly
  less than max_attempts, it becomes Queued again; otherwise it becomes Failed
  and releases capacity. Return true in either accepted case.
- Requeuing preserves ID, payload, priority and original submission position;
  attempts increase only on the next successful `start_next()`. max_attempts
  counts total starts, including the first start.
- Completing an unknown, Queued or terminal job returns false unchanged.
- `cancel(id)` only accepts a Queued job. It becomes Cancelled, retains its
  attempts and releases capacity. Unknown, Running and terminal jobs return
  false unchanged. A requeued retry can also be cancelled.
- Succeeded, Failed and Cancelled are terminal: no transition may revive them.

## 3. Deterministic snapshots

- `find(id)` returns a value copy of the full current Job, or `std::nullopt` for
  an unknown ID. `active_count()` returns the number of Queued plus Running jobs.
- `list()` returns copies of all retained jobs, including terminal jobs, sorted
  by case-sensitive ASCII ID order. This reporting order does not affect dispatch.
- `list(status)` filters to exactly that status, keeping the same sorted order.
  No match produces an empty vector.
- Mutating returned values or retaining them across later queue transitions
  must never mutate internal state or retroactively alter earlier snapshots.
- Queues are independent, including capacity, IDs, ordering and retry budgets.

No threads, clocks, disk persistence, real command execution or external job
processes are part of this exercise. All jobs and payloads are synthetic data.
