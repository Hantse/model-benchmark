# Acceptance contract

All calls are synchronous and single-threaded. The supplied Host and Clock outlive
the plugin, and host operations do not throw. Player IDs and all data are synthetic.
The protected header is the complete public contract; implementation state belongs
in `RewardPlugin::State`, defined in `src/reward_plugin.cpp`.

## 1. Safe registration lifecycle

`Load()` must validate `reward_credits` in the inclusive range 1..10000 and
`cooldown_seconds` in 1..86400 before calling the host. Invalid configuration
returns false with no registration attempt. Valid configuration registers exactly
one `/daily` handler that forwards calls to this plugin's `HandleChat`.

The host returns false if that command is already owned. Return false without
replacing or unregistering the existing handler; a later Load on the same plugin
may retry after that conflict is removed. A second Load on an already loaded
plugin returns true and makes no extra registration attempt.

`Unload()` removes only a registration successfully acquired by this plugin,
resets its loaded state and clears cooldown history. Repeated Unload calls have
no further host effects. The destructor performs the same cleanup, so callbacks
cannot outlive the plugin. Preserve unrelated host commands. A new successful
load after unload starts a fresh lifecycle and empty cooldown history.

## 2. Permission-gated daily reward

An unloaded plugin ignores all direct handler calls. For a loaded plugin, a null
player or empty `eos_id` is ignored with no permission, reward or reply operation.
For a valid player, trim leading and trailing ASCII space characters (`' '`) only.
The remaining command must be exactly `/daily`, case-sensitive with no arguments.
Otherwise reply `Usage: /daily` only to the caller and stop before permission
lookup or reward operations.

For a valid command, ask the host for permission `reward.claim` using the caller's
stable EOS ID. On denial reply `Permission denied` and do not grant credits or
consume cooldown eligibility. Do not use the player's address as their identity.

When eligible under feature 3, call `AddCredits` exactly once with the caller's
EOS ID and configured credit amount. False guarantees no award was made in this
test port: reply `Reward unavailable` and leave eligibility unchanged so the next
valid claim can retry immediately. On success record the claim timestamp and
reply `Granted N credits`, with N as decimal configured credits. Replies go only
to the caller. There are no broadcasts or real economy integrations.

## 3. Deterministic per-player cooldown

Use only `Clock::NowSeconds()`, which returns nonnegative whole seconds; never
read system time or sleep. Track the last successful claim independently for
each EOS ID during a loaded lifecycle. A new player is immediately eligible,
including at time zero. Reconnecting with the same EOS ID preserves its cooldown.

Let L be the last successful claim, T the current time and C the configured
cooldown. If T >= L, compute elapsed = T - L. Elapsed >= C is eligible, including
the exact boundary. Otherwise reply `Cooldown: R seconds`, where R = C - elapsed,
without calling AddCredits. This exact text uses `seconds` even when R is 1.

If the clock moves backwards (T < L), remain blocked, reply with the full C
seconds and preserve L. An observation, denial, malformed command or failed award
never moves L. Each new successful award moves L to the current T. Unload clears
all claim history, and handler calls while unloaded remain inert.

## Validation

The protected suite has 36 stable acceptance identities, with no skipped tests.
The starter builds but intentionally fails feature cases. All build and test
commands must succeed for a solution to pass; compilation or a partial test set
is insufficient. Test results use CTest's JUnit report under `TestResults/`.
