# Agent instructions

- Read `FEATURES.md` and `ARCHITECTURE.md` before editing.
- Modify source files under `src/` only, subject to the protected exclusions in `benchmark.json`. You may create C# and Razor implementation files under these directories.
- Keep all project files, NuGet lock files, `Directory.Build.props`, `global.json`, `Benchmark.slnx`, `.gitignore`, Markdown contracts, `benchmark.json` and the complete `tests/` tree unchanged. Dependencies are already present; do not add packages or references.
- Preserve public types and method signatures used by the tests. Keep the Domain / Infrastructure / Business / Host project boundaries and actual Fluxor registration. Infrastructure owns services and repositories; Business owns CQRS handlers; Host owns MVC controllers and HTTP result mapping.
- Consume actual `OneOf` handler results with exhaustive `Match` or `Switch` in controllers. Do not replace typed outcomes with an unused package, a dummy union, exception-only HTTP mapping or business logic in `Program.cs`.
- Use the configured local file-backed SQLite database for work-item persistence. No in-memory repository, static dictionary, SQLite `:memory:` substitute or mocked database in place of the real runtime repository. Atomic ownership/version checks belong in SQL, across independent connections.
- Use immutable Fluxor state, pure reducers and asynchronous effects for Blazor behavior. Do not add `lock`, `Monitor`, `SemaphoreSlim`, `Mutex`, reader/writer locks, blocking task waits or a separate busy/request registry to frontend code. Request/session identities in Fluxor state determine whether work is current. SQLite's own transactional locking is allowed in the backend.
- Do not alter test discovery, assertions, test host behavior, coverage of protected files or output reporting. Do not generate fake TRX/JUnit results or intercept test execution.
- Use only synthetic credentials and local SQLite test/development data. Authentication tokens remain only in the scoped Fluxor circuit's memory. Do not contact external services, deploy applications, rent instances or read unrelated files.
- Run both manifest validation commands. Expected failures in the starter are incomplete features, not instructions to weaken tests.
- Build outputs and test results are disposable ignored artifacts. Never commit generated binaries, `obj/`, `bin/`, `TestResults/` or a reference implementation.
