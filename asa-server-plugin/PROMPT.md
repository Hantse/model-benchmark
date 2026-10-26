# Implement an ASA-style daily-reward plugin core

Complete the C++20 starter's three features: registration lifecycle, the
permission-gated `/daily` reward command, and a deterministic per-player cooldown.
Use the exact behavior in FEATURES.md and preserve the protected public contract.

This is an offline exercise inspired by AsaApi server plugins. The protected Host
is a narrow portable test port; it deliberately substitutes fake players,
permissions, credits, replies and a clock. A passing result validates this core
against its 36 acceptance cases. It does not compile or qualify an ASA DLL,
Unreal/DevKit mod, SDK adapter or running game server.

Read the existing header and tests, implement only `src/`, and make all manifest
validation commands pass without modifying protected files or increasing budgets.
Use standard C++ only, no network, sleeps, external dependencies or real services.
You have a 60-minute implementation budget. Finish with the changed files and the
validation commands you actually executed.
