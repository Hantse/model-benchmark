# Tester rules

Read PROMPT.md and FEATURES.md. Implement only files under `src/` as declared
by benchmark.json. The starter deliberately compiles without implementing the
three requested features. Headers, test identities, tests, harness, CMake and
Markdown are protected. Do not skip, replace or edit tests, produce test reports
from product code, or branch on test names, arguments, process names or files.

Use standard C++20 and the standard library only, with per-instance state and
resource-safe ownership. No external dependencies, network, credentials,
services, actual jobs or rentals. Time advances only through the public API;
do not sleep or use the real clock. No threads are required. Checkpoints are
typed copies and must be validated before replacing live state.

Keep generated files under `obj/` or `TestResults/`. All 108 protected cases must
be discovered and pass without skips. Finish with changed files and validation
commands actually executed. This advanced exercise does not change the V1 queue.
