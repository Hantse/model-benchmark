# Tester rules

Read PROMPT.md and FEATURES.md. Implement only files under `src/` as declared by
benchmark.json. The starter stubs are intentional and already compile. Preserve
the public contract under `include/`, the protected test inventory, CMake
configuration, harness, manifests and Markdown. Do not skip, disable, replace or
rewrite tests, and do not write reports from product code.

Use standard C++20 and the standard library. No external dependencies, networks,
real credentials, services, databases or rentals. Keep queue state per instance;
never branch on test names, process arguments, executable names or test files.
The API is single-threaded; concurrency and persistence are outside this task.
Keep generated files under `obj/` or `TestResults/`. Finish by listing changed
files and validation commands actually executed.
