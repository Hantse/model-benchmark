# Model Benchmark Exercises

This repository contains five self-contained coding exercises for evaluating
models on software development tasks. Each exercise includes a starter project,
English instructions, acceptance tests and a `benchmark.json` manifest.

The starters build, but their feature implementations are deliberately
incomplete. Failing feature tests are the expected starting point. The model's
task is to implement the requested features within the time limit and make every
mandatory test pass without changing the protected tests or contracts.

## Projects

| Folder | Stack | Features to implement | Tests | Agent time limit |
|---|---|---|---:|---:|
| [api-jwt](api-jwt/README.md) | ASP.NET Core API | JWT authentication, private task CRUD, admin summary | 28 | 30 minutes |
| [blazor-tasks](blazor-tasks/README.md) | Blazor Server | Circuit-scoped login session, private tasks and filters, accessible task UI | 18 | 30 minutes |
| [api-react-tasks](api-react-tasks/README.md) | ASP.NET Core API + React | JWT authentication, private task CRUD, React login and task workflow | 35 | 30 minutes |
| [api-react-layered](api-react-layered/README.md) | Layered ASP.NET Core API + React / Redux Toolkit | JWT authentication, private versioned work items, centralized frontend state and HTTP workflow | 73 | 60 minutes |
| [api-blazor-fluxor](api-blazor-fluxor/README.md) | Layered ASP.NET Core API + Blazor / Fluxor | JWT authentication, private versioned work items, circuit-scoped frontend state and HTTP workflow | 82 | 60 minutes |

The three basic exercises focus on authentication/session handling, ownership
and a small task workflow. `blazor-tasks` uses a local session per Blazor circuit;
it has no separate API and does not use JWTs.

The two advanced exercises require API layers for **Domain**, **Application**,
**Infrastructure** and **API**. Application services receive repository interfaces
through dependency injection; Infrastructure implements storage. Version checks
must remain atomic when concurrent requests modify the same work item.
Basic tasks use a completed flag; advanced work items use `Todo`, `InProgress`
and `Done` statuses together with an expected version for mutations.

Their frontends follow a Redux-style pattern: state, actions, reducers and
asynchronous effects. React uses Redux Toolkit and react-redux; Blazor uses
Fluxor with a separate store per circuit. Tests cover pending operations, errors,
logout, unauthorized responses and stale asynchronous completions.

There are **236 mandatory tests** across the repository. Each exercise is an
independent benchmark; select and evaluate one folder per run.

## Files inside each exercise

- `README.md`: project overview, requirements and local commands.
- `PROMPT.md`: the task to give the model.
- `FEATURES.md`: exact behavior and acceptance requirements.
- `AGENTS.md`: implementation rules and protected boundaries.
- `ARCHITECTURE.md`: required layers and state-management contracts in the
  advanced exercises.
- `benchmark.json`: prompt/instruction paths, preparation and validation commands,
  editable/protected paths, expected test count and time limit.
- Source and test folders: the incomplete application and its acceptance suite.
- Lockfiles and build configuration: pinned dependencies and SDK settings.

Read the selected exercise's instructions before changing its source. Preserve
its public interfaces, test hooks and protected configuration.

## Run an exercise locally

Use the .NET SDK specified by each project's `global.json` (10.0.302, with patch
roll-forward enabled). The React exercises also require Node.js 22.12 or newer
and npm. NuGet and npm dependencies are locked.

Open the selected folder and follow its README. For example, from `api-jwt`:

```sh
dotnet restore Benchmark.slnx --locked-mode
dotnet build Benchmark.slnx --no-restore
dotnet test Benchmark.slnx --no-restore --logger "trx;LogFileName=acceptance.trx" --results-directory TestResults
```

The other projects have their own frontend commands and report paths. The
Blazor/Fluxor exercise validates API and frontend test projects separately.
Use the selected manifest's commands for the final benchmark verdict.

All accounts, keys and data in these exercises are synthetic. No production
credentials, database or cloud service is required by the applications.

## Register an exercise in the benchmark admin

Use this repository URL:

```text
https://github.com/Hantse/model-benchmark.git
```

Pin an exact 40-character Git commit containing the chosen exercise, then set
its repository-relative manifest path:

```text
api-jwt/benchmark.json
blazor-tasks/benchmark.json
api-react-tasks/benchmark.json
api-react-layered/benchmark.json
api-blazor-fluxor/benchmark.json
```

The worker uses the selected manifest's parent folder as the exclusive workspace
root. All prompt, instruction, source and command paths resolve from that folder.
Sibling exercises, this overview and Git history are excluded from the model's
workspace. Selecting a preset does not verify that its folder exists at a commit.

Agent time limits in the table apply to implementation work. Dependency setup,
model loading, final evaluation and result retrieval need separate runtime and
rental budgets. Saving a tester definition does not launch a model or rent an
instance.

A successful solution must discover and pass every expected test, with no skipped
tests and successful validation commands. Reports use TRX for .NET and JUnit for
React. Compilation alone is not a passing result; a setup failure is an
environment incident rather than a feature-test result.

Keep generated outputs (`bin`, `obj`, `node_modules`, `dist`, `TestResults`) and
reference solutions out of the repository. Do not publish reference solutions in
Git history or use benchmark exercises and solutions as training data.
