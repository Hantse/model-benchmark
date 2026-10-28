# Advanced offline ASA reward-plugin contract

Exactly three features, 108 mandatory tests and 60 minutes of agent work. Calls
are synchronous and single-threaded. Host/Clock outlive the plugin; host methods
do not throw or reenter callbacks. Registrations return distinct nonzero opaque
handles, or nullopt with no registration acquired. No real server, SDK, DLL,
network, disk, threads or wall clock. This is an independently written test port.

## 1. Atomic lifecycle, ownership and validated live configuration

Valid Config: operation_namespace is 1..32 bytes of ASCII A-Z/a-z/0-9/_/-;
reward_credits 1..10000; cooldown_seconds 1..86400; retry_seconds 1..3600;
max_attempts 1..10; pending_limit 1..1024. Construction stores configuration
without calling Host or Clock. Invalid configuration makes Load return false
before any registration. Initial observed_time is zero and next_operation one.

Load registers, in order, `/daily`, `/daily-status`, then one timer. Chat
callbacks forward to HandleChat and the timer to Tick. Success commits the
loaded lifecycle. If any step fails, remove only acquired handles in reverse
order; preserve foreign registrations and saved state. A later Load can retry.
Load while loaded returns true without extra host calls. Unload while loaded
invalidates callbacks before removing timer, status command, daily command in
that order, and clears player records. Preserve observed_time and next_operation.
Repeated Unload while unloaded does nothing, including after Restore. Destruction
performs cleanup. Copies of old callbacks must remain inert after rollback,
Unload, destruction, and a later successful Load on the same object.

Reload accepts only a loaded plugin, a valid Config with unchanged namespace,
and pending_limit >= the count of Pending+Uncertain records. On rejection change
nothing. Success replaces configuration without registration, time sampling or
state loss. Already admitted claims keep all captured reward/retry/cooldown
parameters; new claims use the new configuration.

Loaded direct chat calls first validate the player: null, empty, >64 bytes or
non-ASCII EOS IDs are ignored without any Host/Clock action. EOS grammar is the
same as namespace, with length 1..64; identity is case-sensitive and stable
across reconnects. Trim ASCII spaces only. Exactly `/daily` and `/daily-status`
are accepted; everything else replies `Usage: /daily | /daily-status` only,
without permission lookup or time sampling. `/daily` asks for `reward.claim`;
status asks for `reward.status`. Denial replies `Permission denied` and stops.
An unloaded plugin ignores chat and timer calls entirely.

Status is read-only (no clock/economy calls). Reply `No claim` if unknown;
otherwise `Pending NS:N`, `Granted NS:N` or `Failed NS:N`, where Uncertain is
also displayed as Pending. NS is configured namespace and N decimal operation.

## 2. Fenced, idempotent economy reconciliation and deterministic retries

For an authorized daily request or loaded Tick, observe only Clock.NowSeconds
and set observed_time = max(previous observed_time, observed clock). Backward
clock movement never rewinds state. All time addition saturates at UINT64_MAX.
For daily, an existing Pending/Uncertain claim replies `Pending NS:N` without
processing it. Otherwise a previous success blocks until elapsed >= its captured
last_cooldown_seconds, replying `Cooldown: R seconds` (even for R=1). A new EOS
ID is eligible at time zero. Denials, queries and failed awards do not start a
cooldown. Only a confirmed grant updates last_success_at to observed_time and
last_cooldown_seconds to that claim's captured cooldown.

At most pending_limit records may be unresolved, and at most 10000 distinct EOS
records retained. Bounds reject a NEW claim with `Reward capacity reached`
without consuming a counter or altering a record. Reusing an existing terminal
EOS record does not grow the retained population. IDs never wrap: if next_operation
is UINT64_MAX, reply `Operation IDs exhausted` unchanged. Priority is pending
capacity/retained bound before counter exhaustion. The next admitted claim uses
next_operation then increments it; NS:N is the Host's idempotency key. Capture
the current config's four economy parameters, created_at/ready_at=observed_time,
attempts zero, Pending, and empty timestamps. Preserve any previous success.
Insert it BEFORE processing its first award.

Each processing step sets last_processed_at to observed_time and makes at most
ONE economy call. Pending increments attempts once, then calls AddCredits with
the same stable NS:N, EOS and captured credits. Granted resolves Granted;
Rejected guarantees no credit was added: if attempts < captured max_attempts,
remain Pending with ready_at=now+captured retry_seconds, else resolve Failed.
Unknown means an award may have happened: become Uncertain and schedule retry.
Uncertain MUST call QueryAward, never AddCredits directly. Query Granted resolves
Granted without another award; Unknown remains Uncertain and schedules retry;
Absent returns Pending with a retry delay, unless the attempt budget is exhausted,
in which case resolve Failed. Queries do not consume attempts. Host guarantees
idempotency by operation ID and authoritative Granted/Absent answers; this
synthetic guarantee is not a claim about an actual economy SDK.

Tick processes due unresolved records in ASCII EOS order. ready_at <= now is
due, but a record may be processed at most once at the same observed timestamp.
Do not catch up multiple retry intervals in one Tick. No economy action is made
on an unready record. Resolved records never process again. A newly admitted
claim is processed immediately once; its chat reply is `Granted N credits` on
success, `Reward unavailable` on Failed, or `Pending NS:N` otherwise. Timer
processing sends no chat replies. Only the caller receives command replies.
resolved_at is set only on Granted/Failed, to the observed confirmation/failure
time. Every new claim gets a new operation ID, including after a definitive
failure or an Unload/Load cycle on the same plugin instance.

## 3. Canonical snapshots and atomic recovery without duplicate grants

Save is read-only and returns deep copies of current Config, observed_time,
next_operation and records in strictly increasing ASCII EOS order. It does not
sample Clock or call Host, and works loaded or unloaded. Restore is allowed only
while unloaded; it must validate everything before replacing any state or token
high-water mark, without Host/Clock calls. Reject unless ALL of these hold:

- Snapshot config is valid and exactly equals the receiver's current config;
  at most 10000 records and at most pending_limit unresolved records exist.
- EOS IDs are valid and strictly sorted/unique. Every claim operation is nonzero,
  unique and strictly below nonzero next_operation. All captured economy values
  satisfy Config's respective bounds. Unknown ClaimStatus values reject.
- created_at <= saved observed_time; ready_at >= created_at. If present,
  last_processed_at is in [created_at, observed_time]. attempts <= captured max.
  attempts zero requires no last_processed_at. Positive attempts require it,
  except a recovered Uncertain claim may omit it when ready_at <= observed_time.
- Pending requires attempts < max and no resolved_at. Uncertain requires attempts
  >=1 and no resolved_at. Granted requires attempts >=1; Failed requires attempts
  exactly max. Both terminal states require resolved_at in [created_at, observed_time],
  a present last_processed_at <= resolved_at, and ready_at <= resolved_at.
  Unresolved claims with a present last_processed_at require ready_at >= it.
- last_success_at, if present, is <= observed_time and has cooldown 1..86400;
  if absent its cooldown must be zero. A Granted claim's success time equals its
  resolved_at and success cooldown equals its captured cooldown. For any other
  claim with an earlier success, that success is <= created_at and the elapsed
  interval to created_at is >= the previous success cooldown.

On successful Restore preserve max(receiver observed_time, saved observed_time)
and max(receiver next_operation, saved next_operation). Adopt validated records.
Every unresolved claim with attempts >0 becomes Uncertain and ready immediately
at the adopted time, clearing last_processed_at so one reconciliation can run.
Unattempted Pending claims keep their ready_at raised to at least adopted time.
Terminal records retain all fields. No award or query happens during recovery.
Restoring a fresh object and later Load follows the same rules. Restoring an old
snapshot on the same instance must never reuse an issued operation ID. Save,
invalid Restore and failed Load must not consume IDs or lose restored history.

All data and snapshots are synthetic in-memory values. Tests include combined
workflows, generated scenarios, rollback, stale callbacks, uncertain awards,
integer boundaries and resource cleanup. All 108 cases are visible; no hidden
population, actual persistence or production exactly-once guarantee is claimed.
