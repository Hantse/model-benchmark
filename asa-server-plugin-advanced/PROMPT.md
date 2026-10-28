# Advanced ASA-style daily reward plugin core

Implement the three features in FEATURES.md: atomic owned lifecycle with safe
stale callbacks and live configuration; idempotent rewards with reconciliation
of uncertain outcomes and bounded retries; validated snapshots and replay-safe
recovery. Preserve the protected public API, tests, runner and instructions.

Only src/ is editable. The starter builds but leaves the features incomplete.
Use standard C++20 and the injected Host/Clock only. All 108 acceptance cases
must pass without skips, with successful validation commands. You have 60 minutes
of agent work, with setup and final evaluation budgeted separately.

This is a portable, offline benchmark inspired by ASA server plugins. It does
not build a native DLL, use the AsaApi SDK, install a plugin, run an ASA server
or qualify a DevKit mod. The fake economy's idempotency and query guarantees are
explicit exercise assumptions. Read PROVENANCE.md before interpreting the score.
