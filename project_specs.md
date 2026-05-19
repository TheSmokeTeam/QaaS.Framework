# project_specs.md — QaaS.Framework Solution

> Architectural specification for **QaaS.Framework**, the foundational layer
> of the QaaS platform. See `CLAUDE.md` for the AI operating manual, and
> per-project `project_specs.md` files for component details.
> Live docs: <https://docs.qaas.online/>.

## 1. Purpose

Provide the contracts, primitives, and infrastructure that every other
QaaS package builds on:

- The hook surface (Generator / Assertion / Probe / Processor).
- The protocol surface (Reader / Sender / Transactor / Fetcher / chunk
  variants) with 15+ concrete implementations.
- The policy chain.
- A YAML configuration system with placeholder + reference resolution
  and rich DataAnnotations validation.
- A multi-format serialiser registry with null-safe factories.
- An assembly-scanning hook provider.
- Execution-builder scaffolding and Serilog wiring.

Everything domain-specific (concrete generators, mocker stubs, runner
sessions) lives in downstream repos.

## 2. Scope and non-goals

In scope:

- Public contracts consumed by Runner, Mocker, and Common.* packages.
- Reference protocol implementations.
- Configuration / serialisation / validation infrastructure.
- Logging defaults.

Out of scope:

- Concrete generators / assertions / probes / processors — those live in
  `QaaS.Common.*` and user assemblies.
- Test orchestration — `QaaS.Runner`.
- Mock-server runtime — `QaaS.Mocker`.
- Wire contracts for the mocker controller — `Qaas.Mocker.CommunicationObjects`.

## 3. Solution structure

| Project | Role | Key types |
|---|---|---|
| `QaaS.Framework.SDK` | Hook contracts, context, data primitives. | `IHook`, `IGenerator`, `IAssertion`, `IProbe`, `ITransactionProcessor`, `Context`, `Data<T>`, `SessionData`, `DataSource`, `DataSourceBuilder`, `BaseHook<TConfig>` |
| `QaaS.Framework.Protocols` | Protocol abstractions + 15+ implementations. | `IReader`, `ISender`, `ITransactor`, `IFetcher`, `IChunkReader`, `IChunkSender`, factories, concrete `RabbitMq`, `Kafka`, `Http`, `Grpc`, `Sql`, `Redis`, `MongoDb`, `Elastic`, `Prometheus`, `S3`, `Sftp`, `Socket`, `IbmMq`, `Mocker` (the runner-side controller proxy) |
| `QaaS.Framework.Policies` | Chain-of-responsibility execution control. | `Policy`, `CountPolicy`, `TimeoutPolicy`, `LoadBalancePolicy`, `AdvancedLoadBalancePolicy` |
| `QaaS.Framework.Configurations` | YAML pipeline + validation. | `ConfigurationPlaceHolderParser`, `ConfigurationReferencesParser`, `ValidationUtils`, ~20 custom validation attributes |
| `QaaS.Framework.Serialization` | Multi-format serialisation. | `SerializationType`, `SerializerFactory`, `DeserializerFactory`, per-format implementations |
| `QaaS.Framework.Providers` | Assembly scanning + hook discovery. | `HookProvider<THook>`, `HooksFromProvidersLoader`, `ByNameObjectCreator` |
| `QaaS.Framework.Executions` | Execution-builder scaffolding + logging. | `BaseExecution`, `BaseExecutionBuilder<TContext, TExecutionData>`, `ExecutionLogging` |
| `QaaS.Framework.Infrastructure` | Pure leaf utilities. | `FileSystemExtensions`, `DateTimeExtensions`, `TimeZoneInfoResolver` |
| `QaaS.Framework.Documentation` | Documentation outputs / tooling support. |
| `QaaS.Framework.ElasticBootstrap` | Helpers for Elastic configuration consumed by Executions. |
| `QaaS.Framework.*.Tests` | Test projects (xUnit/NUnit). |

Dependency graph (acyclic): `Infrastructure → {Serialization,
Configurations} → SDK → Protocols → Policies (parallel) → Providers →
Executions`.

## 4. Public surface

### 4.1 Hook contracts

(See `CLAUDE.md` for code; here we summarise.)

- All hooks implement `IHook`. Lifecycle: instantiate → inject `Context`
  → `LoadAndValidateConfiguration` → consumer-specific method.
- `LoadAndValidateConfiguration` returns `List<ValidationResult>?` —
  `null` means "no errors", an empty list is a contract violation.

### 4.2 Protocol contracts

`IConnectable` (Connect/Disconnect) is the base. Specialisations are
named for their semantics; factories return null-safe instances.

### 4.3 Policy contracts

`Policy` is `abstract`; subclasses set `Index` to control chain order.
`Add` inserts in ascending Index order. `RunChain` returns `false` when
a `*StopException` was raised inside a policy body — the caller treats
this as "stop iterating".

### 4.4 Configuration

- Placeholder grammar: `${path.to.value ?? default}`.
- Reference grammar: a single keyword inside a list expands to
  referenced items, subsequent indices shift.
- Validation: standard DataAnnotations + framework-specific attributes
  for cross-property and cross-collection rules.

### 4.5 Serialization

`SerializationType` is the canonical enum; factories build the matching
`ISerializer` / `IDeserializer`. The factories are null-safe by design;
do not "fix" them to throw on null.

### 4.6 Providers

`HookProvider<THook>` exposes:

- `IEnumerable<KeyValuePair<string, THook>>` — for DI registration.
- `THook GetSupportedInstanceByName(string name)` — explicit lookup.

### 4.7 Executions

`BaseExecution` is the abstract execution; `BaseExecutionBuilder` is the
abstract builder. Concrete instances live in Runner / Mocker.

`ExecutionLogging.RegisterDefaults(...)` configures Serilog with
Elasticsearch sink defaults; consumers may override.

## 5. Quality requirements

- `nullable enable` + `TreatWarningsAsErrors=true`.
- `csharpier` formatting.
- ≥ 75 % line coverage (CI gate).
- Public API: XML doc comments on every type and member.
- Public contracts must not break SemVer between minor releases.

## 6. Build, packaging, CI

- Target: `.NET 10.0`.
- NuGet identities (one per project where applicable):
  `QaaS.Framework.SDK`, `…Protocols`, `…Policies`, `…Configurations`,
  `…Serialization`, `…Providers`, `…Executions`, `…Infrastructure`,
  `…Documentation`, `…ElasticBootstrap`. All released in lockstep
  (single git tag → all packages).
- CI: `.github/workflows/ci.yml` — restore → build → test → coverage
  → pack-and-push on stable tags. Concurrency group cancels superseded
  runs. Coverage badges tracked on a public Gist.

## 7. Compatibility & versioning

This is the platform's foundation: every wire/contract change is a
breaking change. Coordinate with Runner, Mocker, Common.*, and downstream
user code via a single tag rollout.

## 8. Roadmap signals

- PR #32 — docs CLAUDE / project_specs.
- PR #31 — `ICloneable<T>` deep-clone for builders.
- PR #30 — builder API alpha + configuration update normalisation
  (merged).
- PR #29 — runtime fixes around probes/RabbitMQ/binary deserialisation
  (merged).

## 9. References

- Live docs: <https://docs.qaas.online/framework/>
- Docs source: `qaas-docs/docs/framework/`.
- Consumers: `QaaS.Runner`, `QaaS.Mocker`, `QaaS.Common.*`.
