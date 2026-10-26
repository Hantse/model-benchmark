# Tester rules

Read PROMPT.md, FEATURES.md and PROVENANCE.md. Implement the three features only
inside `src/`. The incomplete starter is intentional and must remain compilable.
The public header, tests, test runner, CMake configuration, manifest, instructions
and validation commands are protected. Do not change interfaces, replace, disable
or skip tests, or synthesize acceptance reports. Do not add dependencies, network
access, a game server, credentials or real player data. Use the injected Host and
Clock; do not read wall-clock time or sleep. Keep generated output in `obj/` and
`TestResults/`. Do not call production endpoints, grant real rewards, install a
plugin or access ARK DevKit. This portable host is a benchmark contract, not the
AsaApi SDK or its ABI. At the end, list changed files and commands actually run.
