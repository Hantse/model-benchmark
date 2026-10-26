# ASA server-plugin offline benchmark

A self-contained C++20 coding exercise with three intentionally incomplete
features: safe chat registration lifecycle, a permission-gated daily reward,
and deterministic per-EOS-ID cooldowns. It has 36 protected CTest acceptance cases.
The initial implementation compiles; feature-test failures are expected.

This exercise targets portable logic commonly needed around an AsaApi server
plugin. It uses a small **benchmark-specific Host**, fake players and a fake clock.
It does not contain the AsaApi SDK, implement an SDK ABI, produce an ASA DLL,
load into ARK, or exercise a DevKit UGC mod. Passing proves the offline contract
only. See PROVENANCE.md for the upstream distinction and source references.

## Requirements and local commands

- C++20 compiler (GCC 12+, Clang 14+, or MSVC with C++20 support).
- CMake and CTest 3.24+ (JUnit output and fresh worker configurations are required).
- Python 3.10+ available as `python3`.

There are no downloaded packages, external test frameworks or network operations.
The manifest specifies Release builds. From this directory:

```sh
cmake -S . -B obj -DCMAKE_BUILD_TYPE=Release
cmake --build obj --config Release --parallel 2
python3 tests/run_tests.py
```

For a native Windows check, `python tests/run_tests.py` also works when that is
the name of the installed interpreter. The Linux worker manifest uses `python3`.

The runner executes CTest, checks all 36 fixed identities and rejects skipped
tests. It emits `BENCHMARK_TEST_RUN_COMPLETE 36` only after a complete real JUnit
report is verified, including an intentionally red baseline. Failed acceptance
tests still return a nonzero exit. Reports are in `TestResults/acceptance.xml`;
generated builds stay in `obj/`. Do not track generated files.

Only `src/` is editable. Public interfaces in `include/`, tests, build scripts,
instructions and the manifest are protected. No reference implementation is
included. The agent's implementation time limit is 60 minutes; setup and final
evaluation have separate worker budgets. The Linux worker needs a C++20 toolchain
and CMake/CTest installed before execution; this exercise installs nothing.

## Benchmark registration

Repository-relative manifest path: `asa-server-plugin/benchmark.json` in the
shared exercise repository. Select an exact published commit containing this
folder before registering a tester. A local candidate is not a published commit.
Saving a tester does not run a model or launch a game server.
