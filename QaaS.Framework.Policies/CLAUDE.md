# CLAUDE.md — QaaS.Framework.Policies

## Purpose

Chain-of-responsibility runtime that controls iteration. Used inside
`QaaS.Runner.Sessions` (per-action policy chain) and inside
`QaaS.Mocker.Stubs` (rate-limited stub responses). Each `Policy` carries
an `Index`; lower indices run first, and `Add` preserves ascending order
on insertion.

## Key types / files

- `Policy.cs` — abstract base. Protected `uint Index`, `Policy? Next`,
  `Policy Add(Policy)`, `void SetupChain()`, `bool RunChain()`.
  `RunChain` returns `false` iff a `StopActionException` was caught.
- `CountPolicy.cs` — Index 0; raises `CountStop` after N iterations.
- `TimeoutPolicy.cs` — Index 0; raises `TimeoutStop` after duration.
- `LoadBalancePolicy.cs` — Index 1; round-robin slot dispatch.
- `AdvancedLoadBalance/` — Index 2; weighted variant.
- `Policies.cs` — composite / aggregate helpers.
- `PolicyBuilder.cs` — fluent construction.
- `Exceptions/StopAction.cs`, `Exceptions/CountStop.cs`,
  `Exceptions/TimeoutStop.cs` — sentinel exception family caught by
  `RunChain`.
- `ConfigurationObjects/`, `Extentions/` — config DTOs and extension
  helpers.

## Conventions

- `Add` inserts in ascending-Index order — never assume FIFO.
- `RunChain` only catches `StopActionException`; everything else
  propagates.
- Stop sentinels are control flow, not error reporting — log only when
  appropriate.
- Policies are short-lived; per-execution state is fine, but cross-call
  mutable state must be explicitly thread-safe.

## Forbidden

- Throwing non-`StopActionException` to signal "stop" — extend the
  sentinel hierarchy instead.
- Holding mutable static state without synchronisation.
- Using `Thread.Sleep` for timing — `TimeoutPolicy` uses proper clocks.
- Bypassing `Add` and rewiring `Next` directly from outside the class.

## Tests

```bash
dotnet test QaaS.Framework.Policies.Tests/QaaS.Framework.Policies.Tests.csproj --nologo
```

NUnit 4. See `QaaS.Framework.Policies.Tests/CLAUDE.md`.
