# CLAUDE.md — QaaS.Framework.Policies.Tests

## Purpose

Test project for `QaaS.Framework.Policies`. Verifies chain ordering by
`Index`, stop-exception semantics (`CountStop`, `TimeoutStop`), and
multi-policy interaction including the load-balance variants.

## Layout

- `PolicyBehaviorTests.cs` — `Add`, `SetupChain`, `RunChain` happy
  paths + ordering invariants.
- `LoadBalancePolicyTests.cs` — slot dispatch and weighted variants
  (`AdvancedLoadBalancePolicy`).
- `PolicyValidationCoverageTests.cs` — config-DTO validation around
  `ConfigurationObjects/`.

## Conventions

- **NUnit 4** + **Moq** (Moq referenced for the few cases that need a
  collaborator double).
- No `GlobalUsings.cs` here — uses the implicit `Using` include in the
  csproj.
- Test doubles for `Policy` subclass the real abstract type with a
  protected `Index` override and a counter-based `RunThis`.
- Stop-exception assertions use `Assert.That(chain.RunChain(),
  Is.False)` rather than catching the exception themselves.
- `TimeoutPolicy` tests use injected clock sources; never `Thread.Sleep`.

## Forbidden

- Real wall-clock waits.
- Catching `StopActionException` outside `RunChain` to "verify" it —
  the public surface is the boolean return.
- Assuming FIFO `Add` order.
- Sharing chains across tests; build a fresh chain per `[Test]`.

## Run

```bash
dotnet test QaaS.Framework.Policies.Tests/QaaS.Framework.Policies.Tests.csproj --nologo
```
