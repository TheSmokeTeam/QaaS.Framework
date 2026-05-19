# CLAUDE.md — QaaS.Framework.Protocols.Tests

## Purpose

Test project for `QaaS.Framework.Protocols`. Covers factory selection,
`IConnectable` lifecycle, and a representative subset of round-trips
across the protocol abstractions. Heavier integration tests that
require real brokers / databases live downstream in
`QaaS.Runner.E2ETests`.

## Layout

- `ProtocolsTests/` — per-protocol fixtures (factory, connect/disconnect,
  read/send/transact/fetch as applicable).
- `TimeZoneInfoResolverTests.cs` — covers the
  `QaaS.Framework.Infrastructure` resolver from a downstream consumer
  perspective (Infrastructure has no own test project today).
- `GlobalUsings.cs` — `global using NUnit.Framework;` plus a shared
  `Globals.Logger` Serilog sink writing to NUnit output.
- `TestResults/` — generated; ignored.

## Conventions

- **NUnit 4** + **Moq**. Constraint-model `Assert.That(...)`.
- External transports are mocked at the client-library level (Moq for
  abstractions, fakes for raw clients). No Docker / no live brokers in
  this project.
- Each test owns its `IConnectable` instance; teardown disposes it.
- Factory selection tests assert on the concrete returned type per
  `SerializationType` and per protocol config.
- Use `Globals.Logger` for the `Context` logger — keeps NUnit output
  searchable.

## Forbidden

- Reaching out to real Kafka / RabbitMQ / SQL / S3 / etc.
- Leaking connections across tests.
- Asserting on protocol-internal field state via reflection — drive
  through the public abstractions.
- Suppressing flaky tests with `[Retry]` / `[Explicit]` instead of
  fixing the race.

## Run

```bash
dotnet test QaaS.Framework.Protocols.Tests/QaaS.Framework.Protocols.Tests.csproj --nologo
```
