# project_specs.md — QaaS.Framework.Protocols

Protocol abstractions and 15+ reference implementations. Consumed by
Runner (sessions/actions) and Mocker (servers and stub I/O).

## Abstractions

- `IConnectable` — `Connect()` / `Disconnect()`.
- `IReader` — single-message consume with timeout.
- `ISender` — single-message publish.
- `ITransactor` — sync request → response.
- `IFetcher` — historical / range collection.
- `IChunkReader`, `IChunkSender` — batch variants.

## Implementations

`Kafka`, `RabbitMq`, `Http`, `Grpc`, `MsSql`, `PostgreSql`, `Oracle`,
`Trino`, `Redis`, `MongoDb`, `Elastic`, `Prometheus`, `S3`, `Sftp`,
`Socket`, `IbmMq`, plus the `Mocker` proxy that the Runner uses to talk
to a paired mocker control plane.

## Factories

`ReaderFactory`, `SenderFactory`, `TransactorFactory`, `FetcherFactory`,
chunk variants. Keyed by `SerializationType` plus protocol-specific
config record. Always null-safe.

## Forbidden in this project

- Tight coupling to specific YAML shapes — let Configurations + DataAnnotations
  do the validation.
- Holding connections open across executions — every connection is owned
  by its `IConnectable` instance and disposed at end-of-scope.
- Sync-over-async — protocols are async-first.

## Tests

`QaaS.Framework.Protocols.Tests` — covers factory selection, lifecycle,
and a representative subset of round-trips (heavier integration tests
live downstream in `QaaS.Runner.E2ETests`).
