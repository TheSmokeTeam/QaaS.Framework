# project_specs.md — QaaS.Framework.Providers

Assembly scanning, hook discovery, hook instantiation.

## Discovery algorithm

1. Collect entry assembly + `AppDomain.GetAssemblies()` + every `*.dll`
   under `BaseDirectory`.
2. Skip unloadable assemblies silently (debug-log only).
3. Sort by priority (`QaaS.*` = 0, `Common.*` = 1, others = 2), then
   ordinal name.
4. For each request: try `FullName`/`AssemblyQualifiedName` (must be
   unique), then simple `Type.Name` with priority-group tie-breaking.
5. Cache results in a thread-safe dictionary keyed by assembly.

## Public types

- `HookProvider<THook>` — entry point.
- `HooksFromProvidersLoader` — wires providers to consumer DI scopes.
- `ByNameObjectCreator` — instantiates hook types reflectively.

## Concurrency

- `_hookTypeCacheLock` guards the per-assembly cache.
- Multiple concurrent callers are safe — first populates, others read.

## Forbidden in this project

- Re-throwing assembly load exceptions; this would prevent legitimate
  hook discovery whenever a single dll is broken.
- Caching hook *instances* (only types are cached; instances live for
  the duration of one execution).

## Tests

`QaaS.Framework.Providers.Tests` — discovery ordering, ambiguity
resolution, instantiation, threadsafety.
