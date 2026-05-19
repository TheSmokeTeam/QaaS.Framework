# CLAUDE.md — QaaS.Framework.Providers

## Purpose

Assembly scanning, hook-type discovery, and reflective hook
instantiation. The bridge between user-supplied `Common.*` / extension
DLLs and the typed hook contracts in `QaaS.Framework.SDK`. Thread-safe
caching of discovered types per assembly; instances are never cached.

## Key types / files

- `Providers/HookProvider.cs` — `HookProvider<THook> : IHookProvider<THook>`.
  Collects entry assembly + `AppDomain.CurrentDomain.GetAssemblies()` +
  every `*.dll` under `BaseDirectory`. Sorts by priority
  (`QaaS.*` = 0, `Common.*` = 1, others = 2) then ordinal name.
  Resolves by `FullName`/`AssemblyQualifiedName` first (must be unique),
  then by simple `Type.Name` with priority-group tie-breaking and an
  informational log when ambiguity is silently resolved.
- `Providers/IHookProvider.cs` — provider contract.
- `HooksFromProvidersLoader.cs` — `LoadAndValidate` orchestration that
  wires providers into consumer DI scopes.
- `HookData.cs` — load-time DTO.
- `ObjectCreation/` — `ByNameObjectCreator` / `IByNameObjectCreator`,
  reflective ctor invocation.
- `Modules/` — Autofac module wiring.
- `CustomExceptions/` — discovery / instantiation errors.

## Conventions

- Cache `Type[]` per assembly under `_hookTypeCacheLock` (using
  `System.Threading.Lock`). First caller populates; others read.
- Unloadable assemblies are skipped silently with a debug log; partial
  `ReflectionTypeLoadException` results are kept and logged.
- After instantiation, the `Context` is assigned **before** the instance
  is returned to the caller.
- Hooks must have a public parameterless constructor.

## Forbidden

- Re-throwing assembly load exceptions — would break legitimate
  discovery whenever a single DLL is broken.
- Caching hook *instances* (only types are cached).
- Hooks not implementing `IHook` directly or transitively.
- Returning an instance whose `Context` has not been set.
- Using `Assembly.Load` with untrusted paths beyond the configured
  `BaseDirectory`.

## Tests

```bash
dotnet test QaaS.Framework.Providers.Tests/QaaS.Framework.Providers.Tests.csproj --nologo
```

NUnit 4 + Moq. See `QaaS.Framework.Providers.Tests/CLAUDE.md`.
