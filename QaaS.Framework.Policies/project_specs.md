# project_specs.md — QaaS.Framework.Policies

Chain-of-responsibility runtime that controls iteration. Used inside
`QaaS.Runner.Sessions` (per-action policy chain) and inside
`QaaS.Mocker.Stubs` (rate-limited stub responses).

## Public types

- `Policy` (abstract) — `Index`, `Next`, `Add(Policy)`, `SetupChain()`,
  `RunChain()`.
- `CountPolicy` — Index 0; raises `CountStopException` after N items.
- `TimeoutPolicy` — Index 0; raises `TimeoutStopException` after N
  duration elapses.
- `LoadBalancePolicy` — Index 1; spreads load across configured slots.
- `AdvancedLoadBalancePolicy` — Index 2; richer load-balance with
  weights.
- `*StopException` family — sentinel exceptions caught by `RunChain`.

## Conventions

- Lower index runs first.
- `Add` inserts maintaining ascending-Index order — never assume FIFO.
- `RunChain` returning `false` means a stop exception was raised.

## Forbidden in this project

- Throwing from `RunChain` with anything other than a `*StopException`.
- Holding mutable state across `RunChain` invocations without explicit
  thread safety.

## Tests

`QaaS.Framework.Policies.Tests` — chain ordering, stop-exception
semantics, multi-policy interaction.
