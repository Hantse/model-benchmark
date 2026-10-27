# Layered API + Blazor Fluxor benchmark

A deliberately incomplete .NET 10 work board. The model must implement three features: synthetic JWT login, private SQLite-backed versioned work items, and a Blazor UI backed by real Fluxor. The starter compiles; its feature tests intentionally fail.

The backend has four separate projects: **Domain / Infrastructure (services and repositories) / Business (CQRS) / Host (MVC controllers)**. Business returns typed OneOf results that controllers exhaustively match into HTTP responses. Program composes the host; it does not implement endpoints. Blazor coordinates requests through immutable Fluxor state, pure reducers and HTTP effects, without locks or semaphores.

The 213 tests cover real SQLite persistence and concurrent connections, injected services, CQRS outcomes, controller dispatch/mapping, HTTP/JWT behavior, scoped Fluxor and rendered components.

## Publish and register

This revision was requested on 27 October 2026. To use it from `https://github.com/Hantse/model-benchmark.git`, register the **exact commit SHA containing this revision** with `api-blazor-fluxor/benchmark.json`. Previously registered testers/runs retain their historical commit and 82-test contract. The manifest's parent becomes the exclusive workspace root: commands and all manifest paths are relative to this folder. Other exercises, enclosing instructions and Git history are excluded. Run the local commands below from this folder.

Read `PROMPT.md`, `FEATURES.md`, `ARCHITECTURE.md` and `AGENTS.md`. The database contains only synthetic local test/development data. There are no real users, production credentials, external database server, cloud services or model weights in this sample.

## Local commands

Prerequisite: .NET SDK 10.0.302 (a later patch in the same feature band is accepted by `global.json`). All NuGet package versions and dependency lock files are included.

```text
dotnet restore Benchmark.slnx --locked-mode
dotnet build Benchmark.slnx --no-restore
dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --logger "trx;LogFileName=api.trx" --results-directory TestResults
dotnet test tests/Frontend.Tests/Frontend.Tests.csproj --no-restore --logger "trx;LogFileName=frontend.trx" --results-directory TestResults
```

Expected starter result: **213 discovered tests, 28 passed, 185 failed, 0 skipped**. Build and locked restore succeed; failed feature assertions are the intended starting point. The acceptance target is 213 passed and no failures or skipped tests. Backend: 162 tests. Frontend: 51 tests. TRX files are written under `TestResults/`.

A private reference was checked on Windows with the same protected tests/configuration and passed all 213 tests without skips. Its solution is excluded from this exercise. This qualification establishes feasibility; it is not a result achieved by a benchmarked model. The previous 82-test in-memory qualification remains historical evidence, not validation of this revision. See [revision evidence](https://github.com/Hantse/local-ai/blob/master/benchmarks/BLAZOR_SQLITE_VALIDATION.md) in the separate infrastructure repository (not required in an isolated benchmark workspace).

| Coverage | Cases |
| --- | ---: |
| Real SQLite file, persistence, isolation, SQL values and concurrent writes | 36 |
| Infrastructure work-item service with injected repository | 25 |
| Work-item CQRS handlers and typed error cases | 21 |
| MVC controller dispatch/result mapping and compiled OneOf use | 20 |
| Authentication service, CQRS and controller | 18 |
| HTTP/JWT integration, configured database and host restart | 37 |
| Project boundaries | 5 |
| Fluxor reducers/store/effects, adapter and rendered Blazor components | 51 |

## Run the applications after implementing the features

Set synthetic development JWT configuration through environment variables, then start the API on port 5080. Use a test-only signing key of at least 32 UTF-8 bytes.

```text
Jwt__SigningKey=<synthetic-test-only-key-at-least-32-bytes>
Jwt__Issuer=benchmark-work-api
Jwt__Audience=benchmark-work-client
ConnectionStrings__WorkItems=Data Source=work-items.db
dotnet run --project src/Host/Host.csproj --no-restore --urls http://localhost:5080
dotnet run --project src/Frontend/Frontend.csproj --no-restore --urls http://localhost:5081
```

`Backend__BaseUrl` optionally overrides the frontend's API base URL (default `http://localhost:5080`). These environment variable examples use assignment notation; set them using your shell's normal environment syntax. The server-side Blazor circuit calls the API over HTTP; no browser CORS configuration or token storage is needed.

Initialize the configured SQLite schema without clearing existing rows. A restart
with the same database path must retain items and versions. Generated SQLite
files, sidecars and test/build outputs are ignored and must not be committed.

Keep model evaluation separate from this README's local qualification. A passing public test suite does not prove security against adversarial code running inside the test process or completeness beyond the stated contract.
