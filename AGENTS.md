# AGENTS.md — QaaS.Framework
Guidance for AI agents working in this repository.

## What this repo is
QaaS.Framework is the foundational contract and utility layer for the entire QaaS platform (Tier 0).
It ships 8 NuGet packages (SDK, Protocols, Policies, Configurations, Serialization, Providers,
Executions, Infrastructure) consumed by every downstream repo. Breaking changes here ripple across
Runner, Mocker, all Common.* hook libraries, and every consumer project. Target: net10.0.

## Projects / Layout
| Project | Purpose |
|---|---|
| QaaS.Framework.SDK | Core hook abstractions (IHook, IGenerator, IAssertion, IProbe, ITransactionProcessor) + session/data objects |
| QaaS.Framework.Protocols | Messaging, HTTP/gRPC, database, storage abstractions |
| QaaS.Framework.Policies | Chain-of-responsibility policy engine |
| QaaS.Framework.Configurations | Custom YAML loader with `${...}` placeholder interpolation |
| QaaS.Framework.Serialization | Null-safe serializer registry |
| QaaS.Framework.Providers | Assembly-scanning hook discovery: QaaS.* → Common.* → user libs |
| QaaS.Framework.Executions | Execution engine pieces |
| QaaS.Framework.Infrastructure | Base utilities |
| `*.Tests` | Per-project NUnit test projects |

Flat layout — `QaaS.Framework.sln` at root, no `src/` directory. `Directory.Build.props` at root.
Dependency DAG: Infrastructure → Serialization/Configurations → SDK → Protocols/Policies → Providers → Executions.

## Build & test
```shell
dotnet build -m QaaS.Framework.sln
dotnet test QaaS.Framework.sln
# CI coverage (windows-latest; 75% threshold is for badge colouring only, not a build gate)
dotnet-coverage collect "dotnet test QaaS.Framework.sln" -f cobertura -o coverage.xml
reportgenerator -reports:coverage.xml -targetdir:coverage-report
```

## Critical gotchas
- **Tier-0 — everything depends on this.** Any interface change in SDK
  (`IHook`, `IGenerator`, `IAssertion`, `IProbe`, `ITransactionProcessor`) is a breaking change
  for Runner, Mocker, all Common.* libraries, and every consumer project — coordinate releases.
- **CI collects and reports line coverage** (windows-latest, dotnet-coverage + reportgenerator). 75% is used for badge colouring only — there is no threshold step that fails the build.
- **Assembly scanning order is load-order-sensitive**: Providers resolves hooks by `FullName`/`AssemblyQualifiedName` first, then falls back to `Type.Name`. Assembly-name priority (`QaaS.*` → `Common.*` → user libs) is used as a tie-breaker when multiple assemblies expose a hook with the same simple type name; an info log is emitted when a tie is resolved.
- **`ICloneable<T>` deep-clone pattern** (PR #31) is required for builder config objects —
  new config classes must implement deep clone.
- **`${...}` placeholder interpolation** lives in QaaS.Framework.Configurations — do not
  replicate ad-hoc string expansion elsewhere.
- **No `src/` directory** — all projects sit directly under repo root; update sln when adding projects.
- `Directory.Build.props` applies shared MSBuild props to every project; check it before adding
  per-project `<PropertyGroup>` entries to avoid duplication.

## Process
Follow the QaaS harness pipeline: plan → contract → implement → adversarial evaluation
(rubric: Correctness/Completeness/Craft/Robustness, each ≥7/10). Write tests first (TDD).
Conventional commits: `feat:`, `fix:`, `chore(release):`, `fix(ci):`.
Run `dotnet format` before committing.
