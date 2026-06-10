Read `AGENTS.md` at the repo root first — it contains the authoritative hook interface
contracts, build commands, coverage gate, and the cross-repo impact rules for this Tier-0 library.

## Essentials
- **TFM**: net10.0; C# nullable + ImplicitUsings enabled.
- **Test framework**: NUnit 4.x + Moq; **CI gate ≥75% line coverage** (windows-latest).
- **Build**: `dotnet build -m QaaS.Framework.sln` — flat layout, no `src/` directory.
- **Test**: `dotnet test QaaS.Framework.sln`.
- **Key interfaces** (in `QaaS.Framework.SDK/Hooks/`): `IGenerator`, `IAssertion`, `IProbe`,
  `ITransactionProcessor` — any change is a breaking change for Runner, Mocker, and all
  Common.* hook libraries.
- **Hook discovery** (QaaS.Framework.Providers): scans `QaaS.*` → `Common.*` → user libs;
  namespace and assembly naming must follow this order.
- **Commits**: conventional style (`feat:`, `fix:`, `chore(release):`); run `dotnet format`
  before committing.
