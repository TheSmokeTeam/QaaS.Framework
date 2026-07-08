Read `AGENTS.md` at the repo root first — it contains the authoritative hook interface
contracts, build commands, coverage commands, and the cross-repo impact rules for this Tier-0 library.

## Essentials
- **TFM**: net10.0; C# nullable + ImplicitUsings enabled.
- **Test framework**: NUnit 4.x + Moq; CI collects line coverage (windows-latest) — 75% is badge-colouring only, not a build-fail threshold.
- **Build**: `dotnet build -m QaaS.Framework.sln` — flat layout, no `src/` directory.
- **Test**: `dotnet test QaaS.Framework.sln`.
- **Key interfaces** (in `QaaS.Framework.SDK/Hooks/`): `IGenerator`, `IAssertion`, `IProbe`,
  `ITransactionProcessor` — any change is a breaking change for Runner, Mocker, and all
  Common.* hook libraries.
- **Hook discovery** (QaaS.Framework.Providers): resolves by `FullName`/`AssemblyQualifiedName` first, then `Type.Name`; assembly-name priority (`QaaS.*` → `Common.*` → user libs) is the tie-breaker for duplicate simple type names — not a namespace convention requirement.
- **Commits**: conventional style (`feat:`, `fix:`, `chore(release):`); run `dotnet format`
  before committing.
