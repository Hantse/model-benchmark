# C++20 advanced job queue

Implement the three features in FEATURES.md using the protected public API:

1. Atomic batch admission of a dependency graph and deterministic priority/FIFO
   dispatch of eligible jobs.
2. Explicit logical time, fenced execution leases, heartbeats, bounded delayed
   retries and transitive failure/cancellation propagation.
3. Validated deep-copy checkpoints, atomic recovery of interrupted work and
   protection against old lease tokens being reused after a restore.

The starter builds but leaves the implementation incomplete. Only `src/` is
editable. Preserve all protected contracts, tests, configuration and reports.
Use standard C++20 without third-party libraries. Read the exact boundary and
recovery rules before coding; all time values are synthetic uint64 ticks.

There are 108 mandatory acceptance cases. A successful solution passes all of
them with no skips and successful validation commands. Compilation alone is
insufficient. No real workers, disk persistence or external services are needed.
