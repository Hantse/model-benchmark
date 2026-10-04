# api-react-layered

Self-contained benchmark exercise: layered .NET API and React with Redux Toolkit.
The scaffold builds, but feature implementations are intentionally missing.
Complete the three features in PROMPT.md, FEATURES.md and ARCHITECTURE.md.
All 73 acceptance tests are required, with no skips.

Prerequisites: .NET SDK 10.0.302 (latestPatch), Node >=22.12 and npm, Windows or
Linux. NuGet dependencies and npm dependencies are locked.

```sh
dotnet restore Benchmark.slnx --locked-mode
npm --prefix frontend ci
dotnet build Benchmark.slnx --no-restore
dotnet test Benchmark.slnx --no-restore --logger "trx;LogFileName=acceptance.trx" --results-directory TestResults
npm --prefix frontend run build
npm --prefix frontend test
```

Reports: `TestResults/acceptance.trx` and `TestResults/frontend.xml`. Baseline
feature failures are expected; successful compilation alone is not acceptance.

For a manual run, configure synthetic `Jwt__SigningKey` (at least 64 characters),
`Jwt__Issuer=benchmark-work-board-api` and
`Jwt__Audience=benchmark-work-board-client`. Run:

```sh
dotnet run --project src/WorkBoard.Api --urls http://127.0.0.1:5080
npm --prefix frontend run dev
```

Publish this folder inside `https://github.com/Hantse/model-benchmark.git` and
register an exact commit with `api-react-layered/benchmark.json`. The selected
manifest's parent becomes the exclusive workspace root: all commands, prompt,
instruction and test paths resolve from this folder. Run local commands here.
The benchmark controller freezes the Git URL, commit and manifest path. Each
attempt uses a fresh snapshot of this exercise only. Visible tests
support development; final evaluation uses their protected original versions.
Never publish a reference implementation in this repository or its Git history.
