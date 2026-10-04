# Layered API + Blazor Fluxor benchmark

A deliberately incomplete .NET 10 work board. The model must implement three features: synthetic JWT login, private versioned work items, and a Blazor UI backed by Fluxor. The starter compiles; its feature tests intentionally fail.

This is a larger exercise than the basic API and Blazor samples. It tests HTTP behavior, dependency direction, services through an injected repository, atomic optimistic concurrency, a real scoped Fluxor store, HTTP effects and rendered components.

## Publish and register

Publish this folder inside `https://github.com/Hantse/model-benchmark.git`. Keep its tests and contracts, then register an exact commit SHA with `api-blazor-fluxor/benchmark.json`. The manifest's parent becomes the exclusive workspace root: commands and all manifest paths are relative to this folder. Other exercises, enclosing instructions and Git history are excluded. Run the local commands below from this folder.

Read `PROMPT.md`, `FEATURES.md`, `ARCHITECTURE.md` and `AGENTS.md`. There are no real users, production credentials, database, cloud services or model weights in this sample.

## Local commands

Prerequisite: .NET SDK 10.0.302 (a later patch in the same feature band is accepted by `global.json`). All NuGet package versions and dependency lock files are included.

```text
dotnet restore Benchmark.slnx --locked-mode
dotnet build Benchmark.slnx --no-restore
dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --logger "trx;LogFileName=api.trx" --results-directory TestResults
dotnet test tests/Frontend.Tests/Frontend.Tests.csproj --no-restore --logger "trx;LogFileName=frontend.trx" --results-directory TestResults
```

Expected starter result: **82 discovered tests, 10 passed, 72 failed, 0 skipped**. Build and restore succeed; failed feature assertions are the intended starting point. The acceptance target is 82 passed and no failures or skipped tests. API: 43 tests. Frontend: 39 tests. TRX files are written under `TestResults/`.

A private reference was checked locally with the same protected test/configuration files and passed all 82 tests. Its solution is excluded from the published exercise. This qualification establishes feasibility; it is not a result achieved by a benchmarked model.

## Run the applications after implementing the features

Set synthetic development JWT configuration through environment variables, then start the API on port 5080. Use a test-only signing key of at least 32 UTF-8 bytes.

```text
Jwt__SigningKey=<synthetic-test-only-key-at-least-32-bytes>
Jwt__Issuer=benchmark-work-api
Jwt__Audience=benchmark-work-client
dotnet run --project src/Api/Api.csproj --no-restore --urls http://localhost:5080
dotnet run --project src/Frontend/Frontend.csproj --no-restore --urls http://localhost:5081
```

`Backend__BaseUrl` optionally overrides the frontend's API base URL (default `http://localhost:5080`). These environment variable examples use assignment notation; set them using your shell's normal environment syntax. The server-side Blazor circuit calls the API over HTTP; no browser CORS configuration or token storage is needed.

Keep model evaluation separate from this README's local qualification. A passing public test suite does not prove security against adversarial code running inside the test process or completeness beyond the stated contract.
