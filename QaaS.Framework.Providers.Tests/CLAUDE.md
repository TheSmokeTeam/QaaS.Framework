# CLAUDE.md — QaaS.Framework.Providers.Tests

## Purpose

Test project for `QaaS.Framework.Providers`. Verifies discovery
ordering (priority-group then ordinal name), ambiguity resolution
between simple/full/assembly-qualified names, hook instantiation with
`Context` injection, and threadsafety of the per-assembly type cache.

## Layout

- `ProvidersBehaviorTests.cs` — happy-path discovery + instantiation
  via `HookProvider<THook>`.
- `ProvidersCoverageTests.cs` — ambiguity, missing types, broken
  assemblies, lazy vs eager resolution paths.
- `DuplicateHooks.cs` — fixture types intentionally duplicated across
  fake assemblies to drive the ambiguity-resolution branches.
- `GlobalUsings.cs` — `global using NUnit.Framework;`.

## Conventions

- **NUnit 4** + **Moq**. Constraint-model assertions.
- `Mock<IByNameObjectCreator>` is the standard collaborator double; it
  controls which "fake assemblies" surface which types.
- Fixture hooks implement `IHook` minimally — a parameterless ctor and
  a `Context` setter is enough.
- Tests that exercise threadsafety drive `HookProvider` from multiple
  `Task.Run` callers and assert no exception + consistent results.
- Logging assertions use a captured `ILogger` mock to verify the
  documented "ambiguity resolved by priority" `LogInformation`.

## Forbidden

- Loading real third-party DLLs from disk.
- Asserting on private cache state via reflection — drive through
  `GetSupportedInstanceByName` and observable behaviour.
- Cross-test pollution of `AppDomain.CurrentDomain` — never call
  `Assembly.LoadFrom` from a test.

## Run

```bash
dotnet test QaaS.Framework.Providers.Tests/QaaS.Framework.Providers.Tests.csproj --nologo
```
