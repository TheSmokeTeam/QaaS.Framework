# CLAUDE.md — QaaS.Framework.Executions.Tests

## Purpose

Test project for `QaaS.Framework.Executions`. Covers `BaseExecution`
lifecycle, `BaseExecutionBuilder` fluent composition, CLI parser
construction, and the `ExecutionLogging` Serilog defaults (Console +
optional Elasticsearch sink).

## Layout

- `ExecutionsBehaviorTests.cs` — builder happy-path + lifecycle
  (`Start` → `Dispose`).
- `ExecutionsCoverageEdgeCaseTests.cs` — null configs, missing CLI args,
  Elastic-disabled paths.
- `GlobalUsings.cs` — `global using NUnit.Framework;`.

## Conventions

- **NUnit 4** + **Moq**.
- Logger sinks are inspected via in-memory Serilog sinks or by checking
  configured `LoggerConfiguration` shape; do not write to real Elastic.
- CLI parser tests pass `string[]` arguments directly — no env-var
  side-effects.
- Concrete `BaseExecution` test doubles live inline in the test file
  that needs them; do not promote them to fixtures unless reused.

## Forbidden

- Hitting a live Elasticsearch / Console sink that depends on the host
  environment.
- Calling `Environment.Exit` / process-level CLI helpers.
- Mutating global `Log.Logger` static without restoring it in
  `[TearDown]`.

## Run

```bash
dotnet test QaaS.Framework.Executions.Tests/QaaS.Framework.Executions.Tests.csproj --nologo
```
