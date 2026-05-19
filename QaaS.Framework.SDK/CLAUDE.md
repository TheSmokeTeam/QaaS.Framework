# CLAUDE.md — QaaS.Framework.SDK

## Purpose

The contract layer of the framework. Defines hook interfaces, the runtime
`Context`, the data primitives (`Data<T>`, `DetailedData<T>`,
`SessionData`, `DataSource`), and the `BaseHook<TConfig>` family that
downstream hook implementations inherit. Smallest, most stable project —
behaviour-free beyond hook lifecycle plumbing. Every breaking change
ripples to every consumer (Runner, Mocker, every Common.* package).

## Key types / files

- `Hooks/IHook.cs` — root contract: `Context Context { get; set; }` plus
  `List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration)`.
- `Hooks/Generator/IGenerator.cs`, `Hooks/Assertion/IAssertion.cs`,
  `Hooks/Probe/IProbe.cs`, `Hooks/Processor/ITransactionProcessor.cs` —
  the four hook flavours.
- `Hooks/BaseHooks/StatusCodeTransactionProcessor.cs` — reference base
  hook implementation.
- `ContextObjects/` — `Context`, logger plumbing.
- `DataSourceObjects/` — `DataSource`, `DataSourceBuilder`.
- `Session/` — `SessionData`.
- `AssertionObjects/` — `AssertionStatus`, `Attachment`.
- `ConfigurationObjects/`, `ConfigurationObjectFilters/` — DTO bases for
  hook configurations.

## Lifecycle (consumers MUST honour)

1. `HookProvider` instantiates via parameterless ctor.
2. Caller assigns `hook.Context`.
3. Caller calls `LoadAndValidateConfiguration`.
4. Caller calls the domain method (`Generate` / `Assert` / `Run` /
   `Process`).

## Conventions

- Pure contracts + DTOs. No I/O, no static state.
- `LoadAndValidateConfiguration` returns `null` for "no errors" — never
  an empty list.
- Public surface uses `IImmutableList<T>` for sessions/data-sources
  passed to hook methods.
- XML doc comments on every public member (consumed by docs generator).

## Forbidden

- Adding hook *implementations* — those belong in `Common.*` / user
  assemblies.
- Direct I/O, threading, logging side-effects.
- Returning empty `List<ValidationResult>` from validation — return
  `null`.
- Mutable identity on `record` types — use `record` only for value-like
  DTOs.
- Taking dependencies on Configurations / Protocols / Providers /
  Executions (this project sits beneath them).

## Tests

```bash
dotnet test QaaS.Framework.SDK.Tests/QaaS.Framework.SDK.Tests.csproj --nologo
```

NUnit 4 + Moq. See `QaaS.Framework.SDK.Tests/CLAUDE.md`.
