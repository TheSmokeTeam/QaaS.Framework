# QaaS.Framework

Composable .NET packages for building, running, and validating Quality-as-a-Service (QaaS) test workflows.

[![CI](https://img.shields.io/badge/CI-GitHub_Actions-2088FF)](./.github/workflows/ci.yml)
[![Docs](https://img.shields.io/badge/docs-qaas--docs-blue)](https://thesmoketeam.github.io/qaas-docs/)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

## Contents
- [Overview](#overview)
- [Packages](#packages)
- [Functionalities](#functionalities)
- [Protocol Support](#protocol-support)
- [Quick Start](#quick-start)
- [Build, Test, Coverage](#build-test-coverage)
- [Documentation](#documentation)

## Overview
This repository contains one solution: [`QaaS.Framework.sln`](./QaaS.Framework.sln).

The solution is split into focused NuGet packages (SDK, protocols, policies, configuration, providers, serialization, infrastructure, and execution orchestration) so you can consume only what you need.

## Packages
| Package | Latest Version | Total Downloads | Line Coverage | Branch Coverage |
|---|---|---|---|---|
| [QaaS.Framework.Configurations](https://www.nuget.org/packages/QaaS.Framework.Configurations/) | [![NuGet](https://img.shields.io/nuget/v/QaaS.Framework.Configurations?logo=nuget)](https://www.nuget.org/packages/QaaS.Framework.Configurations/) | [![Downloads](https://img.shields.io/nuget/dt/QaaS.Framework.Configurations?logo=nuget)](https://www.nuget.org/packages/QaaS.Framework.Configurations/) | [![Coverage](https://img.shields.io/badge/coverage-78.80%25-yellowgreen)](./QaaS.Framework.Configurations.Tests/) | `74.13%` |
| [QaaS.Framework.Executions](https://www.nuget.org/packages/QaaS.Framework.Executions/) | [![NuGet](https://img.shields.io/nuget/v/QaaS.Framework.Executions?logo=nuget)](https://www.nuget.org/packages/QaaS.Framework.Executions/) | [![Downloads](https://img.shields.io/nuget/dt/QaaS.Framework.Executions?logo=nuget)](https://www.nuget.org/packages/QaaS.Framework.Executions/) | [![Coverage](https://img.shields.io/badge/coverage-82.14%25-green)](./QaaS.Framework.Executions.Tests/) | `80.00%` |
| [QaaS.Framework.Infrastructure](https://www.nuget.org/packages/QaaS.Framework.Infrastructure/) | [![NuGet](https://img.shields.io/nuget/v/QaaS.Framework.Infrastructure?logo=nuget)](https://www.nuget.org/packages/QaaS.Framework.Infrastructure/) | [![Downloads](https://img.shields.io/nuget/dt/QaaS.Framework.Infrastructure?logo=nuget)](https://www.nuget.org/packages/QaaS.Framework.Infrastructure/) | [![Coverage](https://img.shields.io/badge/coverage-72.22%25-yellowgreen)](./QaaS.Framework.Protocols.Tests/) | `50.00%` |
| [QaaS.Framework.Policies](https://www.nuget.org/packages/QaaS.Framework.Policies/) | [![NuGet](https://img.shields.io/nuget/v/QaaS.Framework.Policies?logo=nuget)](https://www.nuget.org/packages/QaaS.Framework.Policies/) | [![Downloads](https://img.shields.io/nuget/dt/QaaS.Framework.Policies?logo=nuget)](https://www.nuget.org/packages/QaaS.Framework.Policies/) | [![Coverage](https://img.shields.io/badge/coverage-79.88%25-yellowgreen)](./QaaS.Framework.Policies.Tests/) | `63.88%` |
| [QaaS.Framework.Protocols](https://www.nuget.org/packages/QaaS.Framework.Protocols/) | [![NuGet](https://img.shields.io/nuget/v/QaaS.Framework.Protocols?logo=nuget)](https://www.nuget.org/packages/QaaS.Framework.Protocols/) | [![Downloads](https://img.shields.io/nuget/dt/QaaS.Framework.Protocols?logo=nuget)](https://www.nuget.org/packages/QaaS.Framework.Protocols/) | [![Coverage](https://img.shields.io/badge/coverage-84.47%25-green)](./QaaS.Framework.Protocols.Tests/) | `69.05%` |
| [QaaS.Framework.Providers](https://www.nuget.org/packages/QaaS.Framework.Providers/) | [![NuGet](https://img.shields.io/nuget/v/QaaS.Framework.Providers?logo=nuget)](https://www.nuget.org/packages/QaaS.Framework.Providers/) | [![Downloads](https://img.shields.io/nuget/dt/QaaS.Framework.Providers?logo=nuget)](https://www.nuget.org/packages/QaaS.Framework.Providers/) | [![Coverage](https://img.shields.io/badge/coverage-75.43%25-yellowgreen)](./QaaS.Framework.Providers.Tests/) | `80.76%` |
| [QaaS.Framework.SDK](https://www.nuget.org/packages/QaaS.Framework.SDK/) | [![NuGet](https://img.shields.io/nuget/v/QaaS.Framework.SDK?logo=nuget)](https://www.nuget.org/packages/QaaS.Framework.SDK/) | [![Downloads](https://img.shields.io/nuget/dt/QaaS.Framework.SDK?logo=nuget)](https://www.nuget.org/packages/QaaS.Framework.SDK/) | [![Coverage](https://img.shields.io/badge/coverage-80.91%25-green)](./QaaS.Framework.SDK.Tests/) | `70.83%` |
| [QaaS.Framework.Serialization](https://www.nuget.org/packages/QaaS.Framework.Serialization/) | [![NuGet](https://img.shields.io/nuget/v/QaaS.Framework.Serialization?logo=nuget)](https://www.nuget.org/packages/QaaS.Framework.Serialization/) | [![Downloads](https://img.shields.io/nuget/dt/QaaS.Framework.Serialization?logo=nuget)](https://www.nuget.org/packages/QaaS.Framework.Serialization/) | [![Coverage](https://img.shields.io/badge/coverage-75.42%25-yellowgreen)](./QaaS.Framework.Serialization.Tests/) | `61.84%` |

Coverage values are from local `dotnet test` + Coverlet collection run on `2026-03-06`.

## Functionalities
### [QaaS.Framework.Configurations](./QaaS.Framework.Configurations/)
- Loads configuration from YAML files (local file system and HTTP GET sources).
- Resolves placeholders and collapses custom configuration path syntax.
- Binds `IConfiguration` into strongly typed objects (including nested lists/dictionaries).
- Recursively validates configurations with DataAnnotations and custom validation attributes.
- Resolves shared references from external YAML files into target configuration lists.

### [QaaS.Framework.SDK](./QaaS.Framework.SDK/)
- Defines the core hook contracts: `IHook`, `IGenerator`, `IAssertion`, `IProbe`, `IProcessor`, and transaction processors.
- Provides runtime context and execution/session data models.
- Includes `DataSourceBuilder` and serialization-aware session data conversion helpers.
- Adds extension methods for context/data/session/logging workflows.

### [QaaS.Framework.Serialization](./QaaS.Framework.Serialization/)
- Serializer/deserializer factories with pluggable types.
- Supported serialization types: `Binary`, `Json`, `MessagePack`, `Xml`, `Yaml`, `ProtobufMessage`, `XmlElement`.
- Supports per-message type hints via `SpecificTypeConfig`.

### [QaaS.Framework.Protocols](./QaaS.Framework.Protocols/)
- Factories for protocol abstractions: `IReader`, `ISender`, `ITransactor`, `IFetcher`, chunked variants.
- Protocol implementations for messaging, storage, SQL, HTTP, gRPC, and observability systems.
- Includes object-name generation strategies and protocol-specific configuration objects.

### [QaaS.Framework.Policies](./QaaS.Framework.Policies/)
- Chain-of-responsibility policy model (`Policy`, `PolicyBuilder`).
- Rate and stop control policies: `Count`, `Timeout`, `LoadBalance`, `IncreasingLoadBalance`, `AdvancedLoadBalance`.
- Stage-based load balancing with per-stage amount/timeout transitions.

### [QaaS.Framework.Providers](./QaaS.Framework.Providers/)
- Dynamically discovers hook implementations in loaded assemblies.
- Creates hook instances by type name and validates loaded hook configuration.
- Includes Autofac module support for provider wiring.

### [QaaS.Framework.Executions](./QaaS.Framework.Executions/)
- Base abstractions for execution runtime and execution builders.
- CLI parser/help builders (via CommandLineParser).
- Loader pipeline for options validation and Serilog logger construction (including optional Elastic sink).

### [QaaS.Framework.Infrastructure](./QaaS.Framework.Infrastructure/)
- Shared filesystem utilities (safe directory-name normalization).
- Date/time conversion helpers with daylight-saving-aware offset conversion.

## Protocol Support
Supported protocol families in `QaaS.Framework.Protocols`:

| Family | Implementations |
|---|---|
| Messaging / Queueing | RabbitMQ, Kafka, IBM MQ |
| HTTP / RPC | HTTP, gRPC |
| Databases | PostgreSQL, Oracle, MSSQL, Trino, MongoDB, Redis |
| Search / Monitoring | Elasticsearch, Prometheus |
| File / Storage | S3, SFTP, Socket |

## Quick Start
Install package(s):

```bash
dotnet add package QaaS.Framework.SDK
dotnet add package QaaS.Framework.Protocols
dotnet add package QaaS.Framework.Executions
```

Minimal custom generator:

```csharp
using System.Collections.Immutable;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Generator;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;

public sealed class StaticGenerator : IGenerator
{
    public QaaS.Framework.SDK.ContextObjects.Context Context { get; set; } = null!;

    public IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult>? LoadAndValidateConfiguration(
        Microsoft.Extensions.Configuration.IConfiguration configuration) => null;

    public IEnumerable<Data<object>> Generate(
        IImmutableList<SessionData> sessionDataList,
        IImmutableList<DataSource> dataSourceList)
    {
        yield return new Data<object> { Body = "hello-qaas" };
    }
}
```

## Build, Test, Coverage
```bash
dotnet restore QaaS.Framework.sln
dotnet build QaaS.Framework.sln -c Release --no-restore
dotnet test QaaS.Framework.sln -c Release --no-build
```

Coverage command used for this README:

```bash
dotnet test <Project>.Tests.csproj -c Release --collect:"XPlat Code Coverage"
```

## Documentation
- Official docs: [thesmoketeam.github.io/qaas-docs](https://thesmoketeam.github.io/qaas-docs/)
- CI workflow: [`.github/workflows/ci.yml`](./.github/workflows/ci.yml)
- NuGet feed query for all packages: [QaaS.Framework on NuGet search](https://www.nuget.org/packages?q=QaaS.Framework)
