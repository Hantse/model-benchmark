# C++20 job queue benchmark

A deliberately incomplete, buildable C++20 project with three features and 36
independently named acceptance tests. The task covers validated admission,
priority/FIFO scheduling, state transitions, retry budgets and value snapshots.
Read PROMPT.md, FEATURES.md and AGENTS.md before editing `src/`.

## Requirements

- CMake and CTest >= 3.24 (JUnit output and clean reconfiguration are required).
- A C++20 compiler, such as GCC 12+, Clang 14+ or a recent MSVC toolchain.
- Python >= 3.10; Linux benchmark workers expose it as `python3`.

There are no downloaded libraries or network dependencies. The compiler and
CMake/CTest must already be available to the worker. This project does not install
system packages. Toolchain versions belong in the run's environment provenance.

## Local commands

From this exercise directory:

```sh
cmake -S . -B obj -DCMAKE_BUILD_TYPE=Release
cmake --build obj --config Release --parallel 2
python3 tests/run_tests.py
```

On Windows, use `python tests/run_tests.py` if Python is named `python`. The
manifest uses the Linux worker spelling. The protected runner checks the exact
CTest discovery inventory, executes every test and writes `TestResults/cpp.xml`.
It emits the worker's completion marker only after all 36 genuine test cases are
reported with no skips. Test failure returns 1; configuration/compiler failures
are environment incidents, not feature-test results.

The initial feature verdict is intentionally red. A valid solution must compile
and discover/pass all 36 tests without skips, with the protected files unchanged.
The controller allows 60 minutes of agent work; preparation/model loading and
final evaluation need separate budgets. No model is launched by these commands.

For one local diagnostic case after building:

```sh
ctest --test-dir obj -C Release -R cpp.job_queue.retry_limit_is_terminal --output-on-failure
```

Use the full manifest validation commands for the final verdict. Select
`cpp-job-queue/benchmark.json` at an exact shared-repository commit that contains
this folder; a local addition is not a published remote commit. Generated `obj/`
and `TestResults/` content and any private reference solution must stay out of
the exercise repository and training datasets. These in-process assertions
measure functional behavior; they are not an adversarial integrity proof.
