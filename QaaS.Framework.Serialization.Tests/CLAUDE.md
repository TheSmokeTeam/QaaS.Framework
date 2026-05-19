# CLAUDE.md — QaaS.Framework.Serialization.Tests

## Purpose

Test project for `QaaS.Framework.Serialization`. One round-trip
(serialize → deserialize → equal) per `SerializationType`, plus
null-safety on the factories and regression tests for the binary
deserialisation hardening from PR #29.

## Layout

- `SerializationBehaviorTests.cs` — per-format round trips for
  `Binary`, `Json`, `MessagePack`, `Xml`, `XmlElement`, `Yaml`,
  `ProtobufMessage`.
- `SerializationEdgeCaseTests.cs` — null factory inputs, empty byte
  arrays, type-mismatch, binary hardening regressions.
- `GlobalUsings.cs` — `global using NUnit.Framework;`.

## Conventions

- **NUnit 4** + **Moq**. Constraint-model assertions
  (`Assert.That(actual, Is.EqualTo(expected))`).
- Round-trip pattern: build a fresh DTO, serialise, deserialise, assert
  `Is.EqualTo` (DTOs override equality where relevant).
- Factory null-safety pinned by explicit
  `Assert.That(SerializerFactory.BuildSerializer(null), Is.Null)`.
- New formats added to `SerializationType` MUST gain a round-trip test
  here in the same change set.

## Forbidden

- Asserting on serialised byte sequences directly (brittle and
  cross-platform fragile) — drive through the round trip.
- Re-introducing permissive `BinaryFormatter`-style patterns in test
  fixtures.
- Suppressing a failing round trip with custom equality just to make
  the test pass — fix the serialiser.

## Run

```bash
dotnet test QaaS.Framework.Serialization.Tests/QaaS.Framework.Serialization.Tests.csproj --nologo
```
