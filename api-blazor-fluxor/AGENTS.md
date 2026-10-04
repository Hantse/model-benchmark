# Agent instructions

- Read `FEATURES.md` and `ARCHITECTURE.md` before editing.
- Modify source files under `src/` only, subject to the protected exclusions in `benchmark.json`. You may create C# and Razor implementation files under these directories.
- Keep all project files, NuGet lock files, `Directory.Build.props`, `global.json`, `Benchmark.slnx`, `.gitignore`, Markdown contracts, `benchmark.json` and the complete `tests/` tree unchanged. Dependencies are already present; do not add packages or references.
- Preserve public types and method signatures used by the tests. Keep the project reference direction and actual Fluxor registration. Do not replace Fluxor with local component fields or a custom store.
- Do not alter test discovery, assertions, test host behavior, coverage of protected files or output reporting. Do not generate fake TRX/JUnit results or intercept test execution.
- Use only synthetic credentials and local memory storage. Do not contact external services, deploy applications, rent instances or read unrelated files.
- Run both manifest validation commands. Expected failures in the starter are incomplete features, not instructions to weaken tests.
- Build outputs and test results are disposable ignored artifacts. Never commit generated binaries, `obj/`, `bin/`, `TestResults/` or a reference implementation.
