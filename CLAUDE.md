# CLAUDE.md — QaaS.Framework

## Purpose
Core framework providing composable .NET packages for building QaaS test workflows. This is the foundational dependency for Runner, Mocker, and all Common packages.

## Solution Structure
| Project | Purpose |
|---|---|
| `QaaS.Framework.SDK` | Core hook contracts (`IGenerator`, `IAssertion`, `IProbe`, `IProcessor`), context/session/data models, `DataSourceBuilder`, extension methods |
| `QaaS.Framework.Protocols` | Protocol abstractions (`IReader`, `ISender`, `ITransactor`, `IFetcher`) and implementations (RabbitMQ, Kafka, HTTP, gRPC, SQL, Redis, Elastic, S3, SFTP, Socket, MongoDB, IBM MQ) |
| `QaaS.Framework.Policies` | Chain-of-responsibility policy model (`CountPolicy`, `LoadBalancePolicy`, `TimeoutPolicy`, `AdvancedLoadBalancePolicy`) |
| `QaaS.Framework.Configurations` | YAML loading, placeholder resolution, DataAnnotations validation, reference resolution |
| `QaaS.Framework.Serialization` | Serializer/deserializer factories (Binary, Json, MessagePack, Xml, Yaml, Protobuf, XmlElement) |
| `QaaS.Framework.Providers` | Assembly scanning for hook discovery, hook instance creation |
| `QaaS.Framework.Executions` | CLI parser, execution builder, Serilog logger construction |
| `QaaS.Framework.Infrastructure` | Filesystem utilities, date/time helpers |

## Build & Test
```bash
dotnet build QaaS.Framework.sln
dotnet test QaaS.Framework.sln
```

## Key Interfaces
- `IGenerator` — produces `IEnumerable<Data<object>>` from configuration
- `IAssertion` — validates session outputs
- `IProbe` — lifecycle hooks (setup/teardown)
- `IProcessor` / `BaseTransactionProcessor<T>` — mocker request/response transformation
- `ISender` / `IChunkSender` — publish data to protocols
- `IReader` / `IChunkReader` — consume data from protocols
- `ITransactor` — send request, receive response (HTTP, gRPC)
- `IFetcher` — collect data from external APIs
