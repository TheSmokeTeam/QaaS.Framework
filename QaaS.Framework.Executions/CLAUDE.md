# CLAUDE.md — QaaS.Framework.Executions

## Purpose

Execution-builder scaffolding and Serilog wiring shared by Runner,
Mocker, and any future top-level executable. Owns the abstract
`BaseExecution` lifecycle, the fluent
`BaseExecutionBuilder<TContext, TExecutionData>` root, command-line
parsing helpers, and the standard Serilog defaults (Console +
optional Elasticsearch sink).

## Key types / files

- `BaseExecution.cs` — abstract `int Start()` + `IDisposable`.
- `BaseExecutionBuilder.cs` — fluent builder root.
- `IRunner.cs` — runner contract.
- `ExecutionLogging.cs` — `RegisterDefaults(...)` Serilog wiring.
- `CommandLineBuilders/ParserBuilder.cs`,
  `CommandLineBuilders/HelpTextBuilder.cs` — CLI argument parsing.
- `Loaders/BaseLoader.cs` — configuration loader hook point.
- `Logics/`, `Options/`, `Constants.cs` — execution-time helpers.

## Logging defaults

- Console sink, `Information` minimum.
- Optional Elasticsearch sink, index pattern `qaas-{yyyy.MM.dd}`,
  template `qaas`, optional basic auth, certificate validation
  bypassable for dev.
- Filter: only logs carrying a `Team` enrichment land in Elastic.
- Consumers may override per-execution.

## Conventions

- Builders are subclassed per executable; no public `new` on concrete
  `BaseExecution` from foreign assemblies.
- Sink configuration is overridable — never hard-coded.
- CLI surfaces are declarative (see `CommandLineBuilders/`).
- Async-first: `Start()` is sync entry, but inner work flows async via
  policies/protocols.

## Forbidden

- Hard-coding sink endpoints / credentials.
- Creating executions outside the builder pattern.
- `Thread.Sleep` / `Task.Delay` for flow control — that belongs in
  `QaaS.Framework.Policies`.
- Bypassing the `Team` enrichment filter on Elastic-bound logs.

## Tests

```bash
dotnet test QaaS.Framework.Executions.Tests/QaaS.Framework.Executions.Tests.csproj --nologo
```

See `QaaS.Framework.Executions.Tests/CLAUDE.md`.
