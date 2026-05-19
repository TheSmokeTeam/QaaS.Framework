# project_specs.md — QaaS.Framework.Infrastructure

Pure utility leaf project. Zero behavioural state, no external services.

## Public types

- `FileSystemExtensions.MakeValidDirectoryName(string?) → string?` —
  cross-platform sanitiser.
- `DateTimeExtensions.ConvertDateTimeToUtcByTimeZoneOffset(...)` /
  `ConvertUtcDateTimeToTimeZone(...)` — DST-aware conversion.
- `TimeZoneInfoResolver.ResolveTimeZoneInfo(string?)` — wrapper around
  `TimeZoneInfo.FindSystemTimeZoneById`.

## Conventions

- All-static helper classes.
- Cross-platform: must work on Windows and Linux. No hard-coded path
  separators.

## Forbidden in this project

- I/O beyond pure path manipulation.
- Taking dependencies on any other QaaS project (this is the leaf).

## Tests

`QaaS.Framework.Infrastructure.Tests` — covers each helper edge case.
