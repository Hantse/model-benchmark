# Implement a C++20 job queue

Complete the buildable starter in `src/` to implement the three features in
FEATURES.md: validated bounded priority admission, lifecycle with bounded retries,
and deterministic value snapshots. Preserve the API in `include/job_queue.hpp`.

The implementation is deliberately incomplete. Make all 36 mandatory acceptance
tests pass without changing protected files, public signatures or validation
commands. Build and inspect failing tests while working. Use only C++20 and the
standard library; no network or external services are needed.

Run the commands in README.md and report what you actually validated. A compiling
starter alone is not a solved exercise. The agent work budget is 60 minutes;
setup and final evaluation have separate controller budgets.
