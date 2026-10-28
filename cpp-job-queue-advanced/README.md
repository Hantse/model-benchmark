# C++20 advanced job queue

A separate successor to `cpp-job-queue`, retaining three features and the same
CMake/CTest/JUnit workflow. The protected suite grows from 36 to **108 cases**.
The additional difficulty comes from interacting dependency graphs, lease
fencing and recovery invariants. A numerical threefold increase in model
difficulty is a design target, not an empirical measurement.

## Features

- Validate and admit a whole dependency batch atomically, including forward
  references and cycle rejection; dispatch by eligibility, priority and FIFO.
- Track explicit logical time, expiring execution leases, heartbeats and delayed
  retries; reject stale acknowledgements and propagate terminal blockers.
- Validate and restore checkpoints atomically, recover interrupted jobs and
  preserve a token high-water mark against replay of old checkpoints.

See FEATURES.md for the complete contract and include/job_queue.hpp for the API.
All jobs are synthetic and single-threaded. Checkpoints are in-memory values.
No filesystem durability, actual distributed execution or concurrent API is
claimed. Deterministic generated scenarios supplement boundary and workflow
cases; all mandatory tests are visible. No hidden-test facility is implied.

## Local commands

Requires a C++20 compiler, CMake/CTest 3.24+ and Python 3.10+. No test framework
is downloaded. Windows can use an installed Visual Studio CMake generator.

```sh
cmake -S . -B obj -DCMAKE_BUILD_TYPE=Release
cmake --build obj --config Release --parallel 2
python3 tests/run_tests.py
```

The starter is expected to compile and fail feature assertions. The runner
checks the exact 108 CTest identities and genuine JUnit results, rejects skips,
and emits `BENCHMARK_TEST_RUN_COMPLETE 108` only for a complete report. A score
of 100 requires every test and validation command to pass. A failing build or
missing test is never counted as success. The agent time budget is 60 minutes,
the same as V1; setup and evaluation have separate runner budgets.

An optional GCC/Clang diagnostic build supports ASan/UBSan:

```sh
cmake -S . -B obj -DCMAKE_BUILD_TYPE=Debug -DENABLE_SANITIZERS=ON
cmake --build obj --parallel 2
python3 tests/run_tests.py
```

Use a separate clean copy when comparing Release and sanitizer builds. Sanitizer
availability depends on the compiler/runtime; an unsupported diagnostic build
is not a passing check. Mandatory tests also check resource balance on rejected
construction without requiring sanitizer installation.

## Benchmark identity

Repository: `https://github.com/Hantse/model-benchmark.git`.
Manifest: `cpp-job-queue-advanced/benchmark.json`. Pin an exact commit containing
this directory before registration. Merely preparing this folder does not
publish it or run a model. The V1 exercise and historical runs remain separate.
Reference implementations used to qualify the suite must remain outside this
repository and must never enter Git history or training datasets.
