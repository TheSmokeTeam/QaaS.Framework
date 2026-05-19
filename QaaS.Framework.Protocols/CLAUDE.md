# CLAUDE.md — QaaS.Framework.Protocols

## Purpose

Protocol abstractions and 15+ reference implementations covering
messaging, RDBMS, NoSQL, observability, and file-transfer back-ends.
Consumed by Runner (sessions/actions) and Mocker (servers + stub I/O).
Every concrete protocol owns its own connection lifecycle via
`IConnectable`.

## Key types / files

Abstractions (`Protocols/`):

- `IConnectable.cs` — `Connect()` / `Disconnect()`.
- `IReader.cs` — single-message consume with timeout.
- `ISender.cs` — single-message publish.
- `ITransactor.cs` — sync request → response.
- `IFetcher.cs` — historical / range collection.
- `IChunkReader.cs`, `IChunkSender.cs` — batch variants.

Implementations (`Protocols/`): `KafkaTopicProtocol`,
`RabbitMqProtocol`, `HttpProtocol`, `GrpcProtocol`, `MsSqlProtocol`,
`PostgreSqlProtocol`, `OracleSqlProtocol`, `TrinoSqlProtocol`,
`RedisProtocol` / `RedisReaderProtocol`, `MongoDbProtocol`,
`ElasticProtocol`, `PrometheusProtocol`, `S3Protocol`, `SftpProtocol`,
`SocketProtocol`, `IbmMqProtocol`, plus `BaseSqlProtocol` shared base.

Factories (`Protocols/Factories/`): `ReaderFactory`, `SenderFactory`,
`TransactorFactory`, `FetcherFactory`. Always null-safe.

Other: `ConfigurationObjects/`, `Extentions/`, `Utils/`.

## Conventions

- Each protocol implements only the abstraction(s) it can support.
- Connections are owned by the protocol instance and disposed at
  end-of-scope; `await using` for `IAsyncDisposable`.
- Async-first throughout — no sync-over-async.
- Factories key on `SerializationType` plus protocol-specific config
  records; `null` config in → `null` out.
- Validation is delegated to `Configurations` + DataAnnotations on the
  config DTOs; protocols don't re-validate YAML shapes.

## Forbidden

- Holding connections open across executions.
- Sync-over-async (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`).
- `new HttpClient()` per call — use `IHttpClientFactory`.
- Tight coupling to specific YAML shapes inside protocol code.
- Throwing from factories on `null` — return `null`.
- Implementing a new protocol without `Connect`/`Disconnect`.

## Tests

```bash
dotnet test QaaS.Framework.Protocols.Tests/QaaS.Framework.Protocols.Tests.csproj --nologo
```

Heavier integration tests (real brokers/DBs) live downstream in
`QaaS.Runner.E2ETests`. See `QaaS.Framework.Protocols.Tests/CLAUDE.md`.
