# CLAUDE.md — QaaS.Framework.Infrastructure

## Purpose

Pure utility leaf project. Cross-platform helpers for filesystem
sanitisation, DST-aware DateTime conversion, and timezone resolution.
No external services, no QaaS dependencies, no behavioural state. Sits
at the bottom of the dependency graph — every other Framework project
may depend on it; it depends on nothing in QaaS.

## Key types / files

- `FileSystemExtensions.cs` — `MakeValidDirectoryName(string?) → string?`
  cross-platform sanitiser.
- `DateTimeExtensions.cs` —
  `ConvertDateTimeToUtcByTimeZoneOffset(...)` and
  `ConvertUtcDateTimeToTimeZone(...)`, DST-aware.
- `TimeZoneInfoResolver.cs` — `ResolveTimeZoneInfo(string?)` wrapper
  around `TimeZoneInfo.FindSystemTimeZoneById`.
- `IDomainBuilder.cs` — generic builder marker.

## Conventions

- All-static helper classes.
- Cross-platform: works on Windows and Linux. No hard-coded path
  separators — use `Path.*` APIs.
- Nullable annotations on every signature.

## Forbidden

- I/O beyond pure path manipulation.
- Taking a dependency on any other QaaS project — this is the leaf.
- Adding stateful singletons.
- Throwing for null input on sanitisers — return `null`.

## Tests

This project currently has no dedicated test project in the solution.
Coverage comes via downstream consumers (notably
`QaaS.Framework.Protocols.Tests/TimeZoneInfoResolverTests.cs`). When
adding non-trivial behaviour, add a `QaaS.Framework.Infrastructure.Tests`
project rather than expanding downstream tests.

```bash
dotnet build QaaS.Framework.Infrastructure/QaaS.Framework.Infrastructure.csproj --nologo
```
