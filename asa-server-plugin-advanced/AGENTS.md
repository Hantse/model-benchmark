# Tester rules

Read PROMPT.md, FEATURES.md and PROVENANCE.md. Implement only src/. The starter
compiles but is intentionally incomplete. Preserve public headers, tests, cases,
CMake, runner, manifest and Markdown. Never skip/replace tests, synthesize reports
or branch on test names, files, arguments or executable names.

Use standard C++20 only. The Host and Clock are synthetic injected ports, not
AsaApi SDK declarations. No external libraries, server, network, credentials,
real player data, rewards, plugin installation or DevKit access. No sleeps or
wall-clock reads. Keep instance state isolated and ensure old callbacks cannot
access a destroyed or unloaded plugin. Store generated output only in obj/ or
TestResults/. Finish with changed files and actual validation commands.
