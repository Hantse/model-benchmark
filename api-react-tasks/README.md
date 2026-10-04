# api-react-tasks

A self-contained tester in the shared model-benchmark repository. The skeleton
compiles; feature tests intentionally fail until PROMPT.md is implemented.
35 mandatory tests; no skipped tests are allowed.

Requirements: .NET SDK 10.0.302 (latestPatch), Linux or Windows.
Frontend: Node >=22.12 and npm, with dependencies pinned by package-lock.json.

Register `https://github.com/Hantse/model-benchmark.git`, an exact commit SHA,
and `api-react-tasks/benchmark.json`. The worker uses this folder as the exclusive
workspace root. Run the following local commands from this folder.

```sh
dotnet restore Benchmark.slnx --locked-mode
dotnet build Benchmark.slnx --no-restore
dotnet test Benchmark.slnx --no-restore --logger "trx;LogFileName=acceptance.trx" --results-directory TestResults
```

```sh
npm --prefix frontend ci
npm --prefix frontend run build
npm --prefix frontend test
```

Manual run: set the Jwt configuration described in FEATURES.md, then 
`dotnet run --project src/TaskApi --urls http://127.0.0.1:5080`.
In a second terminal: `npm --prefix frontend run dev`.

.NET TRX report: `TestResults/acceptance.trx`.
React JUnit report: `TestResults/frontend.xml`.

The manifest is the v1 evaluation contract. The controller pins the URL and commit;
clone that commit into a new directory for every attempt. The visible tests also
support development; the final evaluator uses their protected versions.
Do not publish any reference solution in this repository's history.
The accounts and keys described here are synthetic benchmark data.
