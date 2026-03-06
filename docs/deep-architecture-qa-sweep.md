# Deep Architecture QA Sweep

## Scope

This review covers the core execution and data flow across the QaaS framework packages and the targeted fixes implemented in `codex/deep-architecture-qa-sweep`.

## Execution Flow

### Configuration and Context

1. `ContextBuilder` composes the effective configuration by layering the base YAML, overwrite YAML files, command-line overrides, optional case YAML, and reference expansion.
2. `ConfigurationPlaceholderParser`, `ConfigurationReferencesParser`, and `ValidationUtils` transform the raw configuration into a validated runtime configuration.
3. `ContextBuilder.BuildInternal()` produces an `InternalContext` that becomes the shared execution state for hooks, data sources, and runners.

### SDK and Data Sources

1. `DataSourceBuilder.Register()` creates `DataSource` definitions with serializer and deserializer settings.
2. `DataSourceBuilder.Build(...)` resolves generator dependencies and filtered upstream data sources.
3. `DataSource.Retrieve(...)` invokes generators, then applies serialization or deserialization transforms before returning execution data.

### Providers and Hook Discovery

1. `HooksLoaderModule<T>` wires `HookProvider<T>` and `HooksFromProvidersLoader<T>` into Autofac.
2. `HookProvider<T>` and `ByNameObjectCreator` discover concrete hook implementations from loaded assemblies.
3. `HooksFromProvidersLoader<T>.LoadAndValidate(...)` instantiates hooks and merges configuration validation results back into the calling pipeline.

### Policies

1. `PolicyBuilder.BuildPolicies(...)` creates policy chains.
2. `Policy.Add(...)` orders the chain using the per-policy `Index`.
3. `Policy.SetupChain()` initializes chain state once before execution.
4. `Policy.RunChain()` executes the policies in order and stops on `StopActionException`.

### Protocols and Persistence

1. `ReaderFactory`, `SenderFactory`, `FetcherFactory`, and `TransactorFactory` select protocol implementations from typed configuration objects.
2. SQL, S3, Elastic, HTTP, gRPC, Kafka, RabbitMQ, Redis, and socket protocols implement the transport-specific persistence or messaging work.
3. Most protocol APIs remain synchronous at the public surface even when they wrap asynchronous client libraries internally.

## DI and Middleware Notes

- This repository is primarily a framework library set, not a hosted ASP.NET application.
- There is no HTTP middleware pipeline in the solution itself.
- Dependency injection is present only in focused areas, mainly the Autofac-based hook loading path in `QaaS.Framework.Providers`.

## High-Impact Findings

### Correctness Bugs

- `Policy.Add(...)` overflowed the stack for chains longer than two items because it recursed against the current node instead of the next node.
- `Policy.SetupChain()` only initialized the first two policies in a chain.
- `AdvancedLoadBalancePolicy` reset itself on every run through virtual `SetupThis()` reuse, which prevented stage progression.
- `AdvancedLoadBalancePolicy` also threw when a stage had valid exit conditions that had not yet been reached, and it could overrun the final stage index.
- `IncreasingLoadBalancePolicy` increased rate before the configured interval elapsed and reset its timer every run, making the ramp-up logic effectively immediate.
- `HttpProtocol.Transact(...)` could degrade into a `NullReferenceException` after transport failure because `null` responses were force-unwrapped.

### Performance Bottlenecks

- `LoadBalancePolicy` used a pure busy-spin loop, burning CPU for every throttle interval.
- `HttpProtocol` built retries around synchronous `.Result` calls, which obscured real exception types and produced weaker retry behavior.
- Shared context dictionary mutation was unsynchronized, which made concurrent execution paths vulnerable to lock-free races while constructing nested state.

### Consistency Findings

- The public API surface is largely synchronous even when it wraps async-capable infrastructure.
- Error handling semantics differed by transport: some code paths returned `null`, some threw domain exceptions, and some leaked low-level null-reference behavior.
- Factory methods relied on null-forgiving operators for required inputs instead of explicit argument validation.

## Implemented Fixes

### Policies

- Fixed recursive chain construction and full-chain initialization.
- Separated one-time policy setup from per-iteration timer restart so advanced policies keep their internal state across runs.
- Corrected advanced load-balance stage progression, final-stage handling, and stage reapplication.
- Corrected increasing load-balance ramp-up timing to honor the configured interval.
- Replaced the throttle busy-spin with cooperative waiting (`Thread.Sleep` plus a short spin for the tail).

### Protocols

- `HttpProtocol` now implements `IDisposable` and disposes its `HttpClient`.
- `HttpProtocol` now creates a fresh request per attempt, catches transport timeouts and request failures directly, and returns a `null` output instead of throwing through a forced null dereference.
- Reader and sender factories now enforce required `DataFilter` inputs explicitly for Elastic and S3 reader paths and return clear errors when a caller requests an unsupported sender mode.

### SDK

- Added locking around shared global dictionary reads and writes in `BaseContext<TExecutionData>` to make nested dictionary mutation safe under concurrent access.

## Test Coverage Added

- Policy regression tests for:
  - multi-node chain setup
  - advanced load-balance stage advancement and final-stage stability
  - increasing load-balance interval-based ramp-up
- Protocol regression tests for:
  - HTTP no-response behavior
  - HTTP disposal behavior
  - factory argument and mode validation
- SDK regression test for:
  - concurrent global dictionary writes and reads

## Residual Risk

- Several protocols still wrap async client APIs synchronously. The current fixes remove the most immediate correctness and CPU issues without breaking the public API, but a larger async-first redesign would still be justified for long-running or high-throughput runners.
