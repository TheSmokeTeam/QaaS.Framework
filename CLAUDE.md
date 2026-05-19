# CLAUDE.md — QaaS.Framework Solution

> Operating manual for AI assistants working in the **QaaS.Framework**
> repository. See `project_specs.md` for the architectural spec, and the
> per-project `project_specs.md` files for details.
> Live docs: <https://docs.qaas.online/>.

## Mission

`QaaS.Framework` is the **foundational layer** of the QaaS platform. It
provides the contracts (hooks, protocols, policies), the configuration
loader, the serialiser registry, the assembly-scanning hook provider, and
the execution scaffolding that `QaaS.Runner`, `QaaS.Mocker`, and every
`QaaS.Common.*` extension package consume. **Every breaking change here
ripples to all downstream repos.**

## Build / Test

```bash
dotnet build QaaS.Framework.sln --nologo -clp:ErrorsOnly
dotnet test  QaaS.Framework.sln --nologo --no-build
csharpier format <changed-files>
```

## Solution layout

| Project | Purpose |
|---|---|
| `QaaS.Framework.SDK` | Core hook contracts (`IHook`, `IGenerator`, `IAssertion`, `IProbe`, `ITransactionProcessor`), `Context`, `Data<T>`, `SessionData`, `DataSource`, the `BaseHook<TConfig>` family, the data-source builder. |
| `QaaS.Framework.Protocols` | Protocol abstractions (`IReader`, `ISender`, `ITransactor`, `IFetcher`, `IChunkSender`, `IChunkReader`, `IConnectable`) and 15+ implementations (Kafka, RabbitMQ, HTTP, gRPC, MS-SQL, PostgreSQL, Oracle, Trino, Redis, MongoDB, Elasticsearch, Prometheus, S3, SFTP, Socket, IBM MQ, Mocker). |
| `QaaS.Framework.Policies` | Chain-of-responsibility policy engine: `Policy` base, `CountPolicy`, `TimeoutPolicy`, `LoadBalancePolicy`, `AdvancedLoadBalancePolicy`. Each policy has an `Index` controlling chain order. |
| `QaaS.Framework.Configurations` | YAML loading, placeholder resolution (`${path ?? default}`), reference resolution, ~20 custom `ValidationAttribute`s. |
| `QaaS.Framework.Serialization` | Multi-format serialisers/deserialisers (`Binary`, `Json`, `MessagePack`, `Xml`, `Yaml`, `ProtobufMessage`, `XmlElement`) with null-safe factories. |
| `QaaS.Framework.Providers` | Assembly scanning, hook discovery, hook instantiation. Threadsafe caching. |
| `QaaS.Framework.Executions` | Execution-builder base classes, CLI parsing helpers, Serilog construction (Console + Elasticsearch sinks). |
| `QaaS.Framework.Infrastructure` | Pure utilities (filesystem, datetime, timezone). Leaf project. |
| `QaaS.Framework.Documentation` | Generated documentation outputs and supporting tooling. |
| `QaaS.Framework.ElasticBootstrap` | Elastic configuration helpers used by Executions. |
| `QaaS.Framework.*.Tests` | xUnit / NUnit test projects, one per non-test project. |

Dependency graph (acyclic):
`Infrastructure → Serialization, Configurations → SDK → Protocols, Policies (independent) → Providers → Executions`.

## Hook contracts (the heart of the framework)

```csharp
public interface IHook
{
    Context Context { get; set; }
    List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration);
}

public interface IGenerator : IHook
{
    IEnumerable<Data<object>> Generate(IImmutableList<SessionData> sessions,
                                       IImmutableList<DataSource> dataSources);
}

public interface IAssertion : IHook
{
    bool Assert(IImmutableList<SessionData> sessions,
                IImmutableList<DataSource> dataSources);
    string? AssertionMessage { get; }
    string? AssertionTrace { get; }
    IList<Attachment>? AssertionAttachments { get; }
    AssertionStatus AssertionStatus { get; }   // Pass | Fail | Skip | Error
}

public interface IProbe : IHook
{
    void Run(IImmutableList<SessionData> sessions,
             IImmutableList<DataSource> dataSources);
}

public interface ITransactionProcessor : IHook
{
    Data<object> Process(IImmutableList<DataSource> dataSources,
                         Data<object> requestData);
}
```

**Lifecycle**:
`HookProvider` instantiates → injects `Context` → calls
`LoadAndValidateConfiguration` → consumer calls
`Generate`/`Assert`/`Run`/`Process`. Hooks are short-lived per execution.

## Protocols

`IConnectable` is the lifecycle base (`Connect()` / `Disconnect()`).
Specialisations:

| Interface | Method | Use |
|---|---|---|
| `IReader` | `Read(TimeSpan)` | Single-message consumer with timeout. |
| `ISender` | `Send(Data<object>)` | Single-message publisher. |
| `ITransactor` | `Transact(Data<object>)` | Synchronous request → response (HTTP, gRPC). |
| `IFetcher` | `Collect(start, end)` | Historical / range-based collector. |
| `IChunkReader` / `IChunkSender` | batch variants | Bulk read/write. |

Concrete implementations under `Protocols/Implementations/...` plus
factories (`ReaderFactory`, `SenderFactory`, etc.) keyed by
`SerializationType`.

## Policies (chain of responsibility)

```csharp
var chain = new CountPolicy(100)
    .Add(new TimeoutPolicy(TimeSpan.FromSeconds(5)))
    .Add(new LoadBalancePolicy(...));
chain.SetupChain();
bool keepGoing = chain.RunChain();   // false ⇒ a *StopException was raised
```

Index ordering: `CountPolicy` and `TimeoutPolicy` share Index 0 (they are
"hard limits"); `LoadBalancePolicy` is Index 1; `AdvancedLoadBalancePolicy`
is Index 2. Lower indices run first.

## Configuration system

YAML pipeline:

1. Load (file, HTTP, embedded resource).
2. **Placeholder resolution**: `${section.path ?? default}` — iterative,
   with circular-reference detection (`_resolutionStack`). Throws
   `InvalidOperationException` on circular refs.
3. **Reference resolution**: list-keyword expansion, with re-indexing of
   subsequent items.
4. **Validation**: `DataAnnotations` + ~20 custom attributes (see below).
5. Bind to C# objects.

Custom attributes (non-exhaustive):

- Dependency: `RequiredIfAny`, `NullIfAny`, `RequiredUnlessAll`.
- Collections: `UniquePropertyInEnumerable`, `AllItemsExistInOther…`.
- Lists: `ValueAppearsInList`, `EnumerablePropertyDoesNotContainAnotherPropertyValue`.
- Paths: `ValidPath`, `AllPathsInEnumerableValid`.
- Comparison: `PropertyComparison(other, op)`.
- Conditional: `ConditionalValidation`, `YamlStringDeserializable`.

## Serialization

`SerializationType` enum: `Binary`, `Json`, `MessagePack`, `Xml`, `Yaml`,
`ProtobufMessage`, `XmlElement`.

Factories are **null-safe**: passing `null` yields `null` rather than
throwing — the consumer is expected to make the explicit decision.

## Providers (assembly scanning)

Algorithm:

1. Collect entry assembly + `AppDomain.GetAssemblies()` + `*.dll` from
   `BaseDirectory`.
2. Skip unloadable assemblies silently (debug-log).
3. Sort: priority (`QaaS.*` = 0, `Common.*` = 1, others = 2) then by
   ordinal name.
4. For each hook type request: attempt FullName / AssemblyQualifiedName
   match (must be unique), then simple `Type.Name` (priority breaks ties).
5. Cache results in a thread-safe dictionary keyed by assembly.

`HookProvider<THook>` is the public entry point; consumers (Runner,
Mocker) consume `IList<KeyValuePair<string, THook>>` via DI.

## Executions

`BaseExecution` (abstract): `int Start()`, `Dispose()`.
`BaseExecutionBuilder<TContext, TExecutionData>` is the public root for
solution-specific builders (Runner, Mocker).

`ExecutionLogging` configures Serilog:

- Console sink at `Information` minimum.
- Optional Elasticsearch sink (index pattern `qaas-{yyyy.MM.dd}`,
  template `qaas`, optional basic auth, certificate validation
  bypassable for dev).
- Filter: only logs carrying a `Team` enrichment land in Elastic.

## Forbidden patterns (NEVER do)

1. Bare `catch` without logging or rethrow.
2. Mutable static fields without `lock` synchronisation.
3. Placeholder resolution that bypasses circular-reference detection.
4. Hook types not implementing `IHook` directly or transitively.
5. Nullable references without explicit null-checks (project enables
   nullable + `TreatWarningsAsErrors`).
6. Serializer/Deserializer factories that throw on `null` input — they
   must return `null`.
7. Protocol implementations without `Connect`/`Disconnect` lifecycle.
8. `LoadAndValidateConfiguration` returning an empty list — return
   `null` for "no errors".
9. Assembly scanning that re-throws load exceptions — log and skip.
10. `Thread.Sleep`/`Task.Delay` in production; flow control belongs in
    `QaaS.Framework.Policies`.
11. Modifying serialised wire shapes (JSON property names, enum values)
    without a coordinated cross-repo bump.

## Must-verify before declaring done

1. `dotnet build` clean.
2. `dotnet test` all green; coverage ≥ 75 % line.
3. Hook implementations inherit `IHook` and have null-safe
   `LoadAndValidateConfiguration`.
4. Hook `Context` is injected before consumer methods are called.
5. Policies sort ascending by `Index`.
6. Placeholder resolution still terminates on the documented circular
   case.
7. Serialiser/deserialiser factories null-safe.
8. Protocols correctly implement Connect/Disconnect.
9. Reference resolution reindexes after a single keyword expansion.
10. CI workflow `.github/workflows/ci.yml` is green.

## Key files

- `SDK/Hooks/IHook.cs` — base hook contract.
- `SDK/Hooks/{Generator,Assertion,Probe,Processor}/I*.cs` — hook
  contracts.
- `Protocols/Protocols/{IReader,ISender,ITransactor,IFetcher}.cs`.
- `Configurations/ConfigurationPlaceHolderParser.cs` — placeholder
  resolution.
- `Configurations/References/ConfigurationReferencesParser.cs` —
  reference resolution.
- `Configurations/ValidationUtils.cs` — DataAnnotations driver.
- `Providers/Providers/HookProvider.cs` — discovery.
- `Providers/HooksFromProvidersLoader.cs` — load-and-validate.
- `Executions/ExecutionLogging.cs` — Serilog wiring.
- `.github/workflows/ci.yml` — CI pipeline.

## Recent / in-flight work

- PR #32 — CLAUDE.md drop (this commit).
- PR #31 — `ICloneable<T>` deep-clone for builders (open).
- PR #30 — builder API alpha + configuration update normalisation
  (merged).
- PR #29 — runtime fixes (probes context, RabbitMQ defaults, binary
  deserialisation hardening) (merged).

## When to update docs

Any change to a hook signature, configuration shape, or protocol
abstraction must be reflected in `qaas-docs/docs/framework/`. The docs
generator (`QaaS.Docs.Generator`) reads XML doc comments — keep them
accurate.
