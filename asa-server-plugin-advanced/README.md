# Advanced C++20 ASA-style reward plugin

A separate advanced version of the offline ASA benchmark: **108 protected tests**
instead of 36, still three features and a 60-minute agent budget.

- Register two commands and a timer atomically, clean up only owned handles,
  invalidate old callbacks and validate live configuration reloads.
- Track rewards by stable EOS identity and idempotency key, reconcile unknown
  outcomes before retrying, preserve captured parameters and monotonic time.
- Validate and restore snapshots atomically without reusing operation IDs or
  blindly repeating a possibly successful award.

See FEATURES.md and include/reward_plugin.hpp for the exact contract, and
PROVENANCE.md for SDK/server limitations. No third-party dependencies, network,
real credentials, game server, threads or sleeps are needed. All mandatory tests
are visible. This is not a loadable ASA DLL or a production economy integration.
The larger population and interacting requirements increase the intended
difficulty; a measured numerical multiplier of model difficulty is not claimed.

## Local validation

Requires a C++20 compiler, CMake/CTest 3.24+ and Python 3.10+. On Windows an
installed Visual Studio CMake generator may be used.

```sh
cmake -S . -B obj -DCMAKE_BUILD_TYPE=Release
cmake --build obj --config Release --parallel 2
python3 tests/run_tests.py
```

The starter compiles and deliberately fails feature tests. The runner checks the
exact executable and CTest inventories, genuine JUnit results and no skips,
then emits `BENCHMARK_TEST_RUN_COMPLETE 108` only for a complete report. All
mandatory cases and validation commands must pass for a successful solution.
Generated files belong under obj/ and TestResults/ only.

Optional memory/undefined-behavior diagnostics use GCC/Clang with a supported
runtime, on a separate clean source copy:

```sh
cmake -S . -B obj -DCMAKE_BUILD_TYPE=Debug -DENABLE_SANITIZERS=ON
cmake --build obj --parallel 2
python3 tests/run_tests.py
```

## Registration identity

Repository: `https://github.com/Hantse/model-benchmark.git`.
Manifest: `asa-server-plugin-advanced/benchmark.json`.
Pin an exact commit containing this folder before registration. Preparation
alone does not publish the exercise or launch a model. The original ASA exercise
and all historical runs stay independent. Keep qualification reference solutions
outside the exercise repository and out of training datasets and Git history.
