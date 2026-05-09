# project_specs.md — QaaS.Framework.Executions

Execution-builder scaffolding and Serilog wiring shared by Runner and
Mocker.

## Public types

- `BaseExecution` (abstract) — `int Start()`, `IDisposable`.
- `BaseExecutionBuilder<TContext, TExecutionData>` (abstract) — root for
  fluent builders.
- `ExecutionLogging` — Serilog defaults helper.

## Logging

`ExecutionLogging.RegisterDefaults(...)` configures:

- Console sink at `Information`.
- Optional Elasticsearch sink (index `qaas-{yyyy.MM.dd}`, template
  `qaas`, optional basic auth, certificate validation bypassable for dev).
- Filter: only logs carrying a `Team` enrichment land in Elastic.

Consumers may override per-execution.

## Forbidden in this project

- Hard-coding sink configuration; everything must be overridable.
- Creating executions outside the builder pattern (no public `new` on
  concrete `BaseExecution` subclasses from foreign assemblies).

## Tests

`QaaS.Framework.Executions.Tests` — builder lifecycle, logger
construction, Elastic sink defaults.
