# CLAUDE.md — QaaS.Framework.SDK.Tests

## Purpose

Test project for `QaaS.Framework.SDK`. Covers data-primitive equality,
`BaseHook<TConfig>` configuration loading, `DataSourceBuilder` round
trips, RabbitMQ metadata serialisation interop, and SDK serialisation
edge cases.

## Layout

- `SDKBehaviorTests.cs` — happy-path lifecycle: hook ctor → context
  injection → `LoadAndValidateConfiguration` → domain method.
- `SDKCoverageEdgeCaseTests.cs` — null inputs, missing config, invalid
  shapes.
- `SDKSerializationCoverageTests.cs` — DTO serialisation contract tests.
- `RabbitMqMetadataSerializationTests.cs` — interop pin for the
  `Common.Protocols.RabbitMq` payload shape.
- `BuildersTests/` — `DataSourceBuilder` and related fluent builders.
- `Globals.cs` — shared Serilog `ILogger` writing to NUnit output.
- `GlobalUsings.cs` — `global using NUnit.Framework;`.

## Conventions

- **NUnit 4** + **Moq 4.20**. No xUnit despite root-CLAUDE phrasing.
- Test fixtures use `[TestFixture]` / `[Test]` / `[TestCase]`.
- `Assert.That(actual, Is.EqualTo(expected))` — Constraint model
  preferred over classic `Assert.AreEqual`.
- Loggers come from `Globals.Logger`; do not stand up a fresh Serilog
  pipeline per test.
- Mock `IConfiguration` via Moq; do not load real YAML files.

## Forbidden

- Adding hook *implementations* to this project — fixtures should be
  minimal stubs, not first-class hooks.
- Real I/O (filesystem, network).
- Suppressing failing tests with `[Ignore]` / `[Explicit]` to make CI
  green — fix the root cause.
- Cross-test mutable static state.

## Run

```bash
dotnet test QaaS.Framework.SDK.Tests/QaaS.Framework.SDK.Tests.csproj --nologo
```

Filter a single fixture: `--filter "FullyQualifiedName~SDKBehaviorTests"`.
