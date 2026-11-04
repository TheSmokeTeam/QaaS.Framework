# project_specs.md — QaaS.Framework.Serialization

Multi-format serialiser / deserialiser registry.

## Formats (SerializationType enum)

`Binary`, `Json`, `MessagePack`, `Xml`, `Yaml`, `ProtobufMessage`,
`XmlElement`.

## Factories

`SerializerFactory.BuildSerializer(SerializationType?)` and the
deserialiser counterpart. **Null-safe**: passing `null` returns `null`
— consumers must handle the explicit decision themselves. Don't "fix"
them to throw.

## Conventions

- Public interfaces: `ISerializer.Serialize(object?) → byte[]?`,
  `IDeserializer.Deserialize(byte[]?, Type?) → object?`.
- Round-trip: every implementation has a "serialize then deserialize
  yields equal" test in the tests project.
- Binary deserialisation returns `null` for null input and otherwise
  returns the payload runtime type; the requested type hint is not used.

## Forbidden in this project

- Side effects (logging, telemetry) inside the hot path.
- New formats without a round-trip test and a factory entry.

## Tests

`QaaS.Framework.Serialization.Tests` — per-format round trips, null
inputs, and binary payload runtime types.
