# Required behavior

Implement the protected C++20 API in `include/job_queue.hpp`. This is a new
exercise, independent of `cpp-job-queue`; there are exactly three features and
108 mandatory tests. All calls are synchronous and single-threaded. State,
logical time and counters belong to each instance. Returned values are deep
copies. No wall clock, sleeps, threads, network, disk persistence or real jobs.
The checkpoint is a typed in-memory value, not a file format.

## 1. Transactional dependency-aware admission and dispatch

Construction requires capacity in 1..10000, max_attempts > 0 and lease_ticks > 0;
otherwise throw `std::invalid_argument` without leaking resources. retry_delay
may be zero. Initial time is zero and both next counters are one.

`submit_batch` admits at most 256 specs atomically. Empty input succeeds without
mutation. Every ID is 1..64 bytes, exclusively ASCII A-Z/a-z/0-9/_/-, case
sensitive. Payload is 1..256 bytes with no NUL, preserved verbatim. Priority is
-100..100 inclusive. A spec has at most 16 dependencies, each a valid distinct
ID, excluding itself. Dependencies may refer to retained jobs or any member of
the same batch, including forward references. Unknown dependencies, cycles,
duplicate/existing IDs, and dependencies already Failed/Cancelled/Blocked reject
the entire batch. Check the whole graph, including disconnected components.

Capacity counts Queued plus Running jobs, including jobs waiting for dependencies
or retry delay. Retain all terminal jobs and reserve their IDs. At most 10000
jobs may be retained, regardless of active capacity. Reject a whole batch if
either bound would be exceeded. Successful jobs receive increasing `order`
values in input order, status Queued, attempts zero, ready_at equal to current
time, and zero lease fields. Store dependencies sorted by ASCII ID. A rejected
operation changes no state, counters, ordering or time.

`claim` selects a Queued job whose dependencies all Succeeded and whose ready_at
is <= current time. Highest priority wins; ties use original order, also after
a retry or restore. Set Running, increment attempts once and assign the next
nonzero queue-wide lease token. Its deadline is saturating(current time +
lease_ticks). Return a copy. No eligible job returns nullopt unchanged.
Counters never wrap: if incrementing next_order for the entire batch would
overflow, reject it; if next_token is UINT64_MAX or the clock is UINT64_MAX,
claim returns nullopt without mutation. The largest assignable counter is thus
UINT64_MAX-1. Failed claims do not consume a token.

## 2. Explicit logical time, fenced leases, retries and failure propagation

`advance_to(tick)` rejects backwards time unchanged. Equal time succeeds. Move
time to tick and process every Running lease with deadline <= tick exactly once.
Expiry is a failed attempt. If attempts < max_attempts, return to Queued with
ready_at = saturating(tick + retry_delay); otherwise become Failed. Clear both
lease fields. The retry delay is measured from the requested tick, not the old
deadline. Advancing far into the future does not execute or repeatedly expire
retries. All uint64 additions saturate at UINT64_MAX, never wrap.

`heartbeat(id, token)` accepts only the exact current Running lease and extends
its deadline to saturating(now + lease_ticks). `complete(id, token, success)`
accepts only that lease. Success makes Succeeded. Failure uses the same bounded
retry rule as expiry, with delay from now. Both reject zero, wrong, stale,
cross-job and non-Running tokens unchanged. Attempts count successful claims,
including the first. Completing or heartbeating never advances logical time.

`cancel` accepts Queued jobs only, including delayed and dependency-waiting jobs;
make Cancelled without increasing attempts and clear lease fields. Unknown,
Running and terminal jobs reject unchanged. Failed, Cancelled and Blocked are
terminal. Whenever a job becomes Failed or Cancelled, recursively mark every
Queued descendant Blocked, clearing leases and preserving attempts/ready_at.
Propagate through multiple levels and shared dependencies. A retrying Queued
parent does not block descendants. Succeeded parents remain satisfied forever.
Blocked/Cancelled/Failed/Succeeded consume no capacity and cannot be revived by
normal operations.

`find` returns a job copy or nullopt. `list` returns all retained jobs or exactly
the requested status, sorted by case-sensitive ASCII ID (independent of dispatch
order). `active_count` counts Queued+Running and `now` returns logical time.

## 3. Validated checkpoints, atomic recovery and replay-safe tokens

`checkpoint` returns all four constructor parameters, clock, next counters and
all jobs sorted by ID, including terminals and sorted dependency lists. Editing
or retaining that value cannot affect a live queue. `restore(saved)` validates
the entire value before changing anything. Reject atomically unless ALL hold:

- Constructor parameters exactly match the receiving queue. At most 10000 jobs
  are present; Queued+Running count does not exceed capacity.
- IDs/specs obey feature 1. Job IDs and dependency lists are strictly ASCII
  sorted, unique and complete. All dependencies exist and the graph is acyclic.
  Unlike new admission, terminal dependencies are permitted in checkpoints.
- Every order is nonzero and unique. next_order is nonzero and greater than
  every order; next_token is nonzero and greater than every nonzero lease token.
  Active Running tokens are unique. Reject unknown enum values.
- attempts <= max_attempts. Queued/Cancelled/Blocked require attempts <
  max_attempts; Running/Succeeded require attempts >= 1; Failed requires attempts
  exactly max_attempts. Only Running jobs have nonzero token/deadline. A Running
  deadline is strictly greater than saved.clock and ready_at <= saved.clock.
- Running/Succeeded/Failed jobs, and any job with attempts > 0, have all
  dependencies Succeeded. Queued jobs have no Failed/Cancelled/Blocked direct
  dependency. Blocked jobs have at least one such direct dependency. These
  rules also imply that a Blocked job has attempts zero.

On successful restore, adopt saved logical time, jobs and next_order. Preserve
the greater of the receiver's previous next_token and saved.next_token: restoring
an old value must never reuse a token previously issued by that receiving queue.
Convert each restored Running job into a failed attempt at saved.clock, applying
the usual retry limit/delay and recursive blocking. Retain its attempt count;
clear token/deadline. Do not dispatch anything during recovery. Existing terminal
jobs remain unchanged. Restoring to a new queue follows the same token rule.
An empty valid checkpoint may replace a nonempty queue. An invalid one must leave
even the receiver's token high-water mark unchanged.

The suite contains boundary, combined workflow, deterministic generated and
large-history cases. Numeric bounds are correctness contracts; no arbitrary
wall-clock performance target or hidden test population is claimed. Passing
108/108 is not a measured threefold increase in model difficulty: this exercise
triples V1's test count and adds interacting graph/time/recovery requirements.
