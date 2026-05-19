# CLAUDE.md — QaaS.Framework.Serialization

## Purpose

Multi-format serialiser / deserialiser registry. Owns the
`SerializationType` enum and produces null-safe `ISerializer` /
`IDeserializer` instances via factories. Consumed by every protocol
that converts in-memory `Data<object>` payloads to wire bytes.

## Key types / files

- `SerializationType.cs` — enum: `Binary`, `Json`, `MessagePack`,
  `Xml`, `Yaml`, `ProtobufMessage`, `XmlElement`.
- `SerializerFactory.cs` — `BuildSerializer(SerializationType?)` →
  `ISerializer?`.
- `DeserializerFactory.cs` — counterpart.
- `Serializers/ISerializer.cs`, `Deserializers/IDeserializer.cs` — public
  contracts.
- `Serializers/{Binary,Json,MessagePack,ProtobufMessage,Xml,XmlElement,Yaml}.cs`
  — implementations; deserialiser counterparts under `Deserializers/`.
- `SerializeConfig.cs`, `DeserializeConfig.cs`, `SpecificTypeConfig.cs`
  — per-format option records.

## Conventions

- Factories are **null-safe**: `null` enum in → `null` out. Consumers
  decide what "none" means.
- Round-trip property (serialize → deserialize → equal) holds for every
  format and has a corresponding test.
- `ISerializer.Serialize<T>(T) → byte[]`,
  `IDeserializer.Deserialize<T>(byte[]) → T?`.
- Binary deserialisation is hardened (PR #29 commit `1365af4`); do not
  re-introduce permissive type binders.

## Forbidden

- Throwing from factories on `null` input — return `null`.
- Side effects (logging, telemetry) in the hot path.
- Adding a new format without (a) a factory entry and (b) a round-trip
  test.
- Loose `BinaryFormatter`-style patterns or unrestricted type binders.
- Mutating `Serialize/Deserialize` signatures without a coordinated
  cross-repo wire-shape bump.

## Tests

```bash
dotnet test QaaS.Framework.Serialization.Tests/QaaS.Framework.Serialization.Tests.csproj --nologo
```

See `QaaS.Framework.Serialization.Tests/CLAUDE.md`.
