# project_specs.md — QaaS.Framework.SDK

The contract layer. Defines `IHook`, `IGenerator`, `IAssertion`, `IProbe`,
`ITransactionProcessor`, the `Context` runtime object, the data primitives
(`Data<T>`, `DetailedData<T>`, `SessionData`, `DataSource`,
`DataSourceBuilder`), and the `BaseHook<TConfig>` family that downstream
hook implementations inherit.

This is the smallest, most stable project of the Framework — intentionally
free of behaviour beyond hook lifecycle plumbing.

## Lifecycle

1. `HookProvider` instantiates a hook type (parameterless ctor expected).
2. Caller assigns `hook.Context = …`.
3. Caller invokes `hook.LoadAndValidateConfiguration(IConfiguration)`.
4. Caller invokes the hook's domain method (`Generate` / `Assert` / `Run`
   / `Process`).

## Forbidden in this project

- Direct I/O — keep this project pure.
- Adding hook *implementations* — those go to Common.* / user assemblies.
- Returning empty validation lists — `null` means "no errors".

## Tests

`QaaS.Framework.SDK.Tests` — covers data primitive equality, `BaseHook`
configuration loading, and `DataSourceBuilder` round-trips.
