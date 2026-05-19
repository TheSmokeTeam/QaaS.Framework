# CLAUDE.md — QaaS.Framework.Configurations

## Purpose

YAML loading, placeholder resolution, reference expansion, and
DataAnnotations-driven validation for the framework. Every QaaS
configuration document — Runner, Mocker, hooks — flows through this
project before binding to typed objects.

## Pipeline (in order)

1. **Load** — file / HTTP / embedded resource (see
   `ConfigurationSources/`, `ConfigurationProviders/`).
2. **Placeholder resolution** — `ConfigurationPlaceHolderParser.cs`
   handles `${section.path ?? default}` iteratively with
   circular-reference detection (`_resolutionStack`); throws
   `InvalidOperationException` on cycles.
3. **Reference resolution** — `References/` expands a single keyword
   inside a list to referenced items, re-indexing subsequent siblings.
4. **Collapse** — `ConfigurationCollapseParser.cs` flattens overrides.
5. **Validation** — `ValidationUtils.cs` drives DataAnnotations + the
   custom attributes in `CustomValidationAttributes/`.
6. **Bind** — `ConfigurationBindingUtils/` produces typed objects.

## Key types / files

- `ConfigurationPlaceHolderParser.cs` — placeholder engine.
- `References/ConfigurationReferencesParser.cs` — reference expansion.
- `ValidationUtils.cs` — validation entry point.
- `ConfigurationUpdateExtensions.cs` — normalised update merge.
- `ConfigurationBuilderExtensions/` — `Microsoft.Extensions.Configuration`
  integration.
- `CustomValidationAttributes/` — ~20 attributes:
  `RequiredIfAny`, `NullIfAny`, `RequiredUnlessAll`, `NullUnlessAll`,
  `UniquePropertyInEnumerable`, `UniquePropertyInEnumerableProperties`,
  `UniqueItemsInEnumerable`, `ValueAppearsInList`,
  `EnumerablePropertyDoesNotContainAnotherPropertyValue`,
  `AllItemsInEnumerablePropertyInEnumerableExistAsPropertyInEnumerable`,
  `AllPathsInEnumerableValid`, `ValidPath`, `PropertyComparison`,
  `ConditionalValidation`, `YamlStringDeserializable`,
  `AtLeastOnePropertyNotNull`, `AtLeastOneEnumerablePropertyNotEmpty`,
  `NoMoreThanXPropertiesNotNull`, `RangeIfAny`,
  `RequiredOrNullBasedOnOtherFieldsConfiguration`.
- `CustomExceptions/`, `YamlConfigurationExceptionFactory.cs` —
  diagnostic surface.
- `DiagnosticMessageFormatter.cs` — error-message formatting.

## Conventions

- Validation methods return `null` for "no errors", never an empty list.
- Custom attributes derive from `ValidationAttribute` and use
  `ValidationValueInspector` for reflective access.
- Placeholder syntax is exclusively `${...}`; no other interpolation.
- Path-style validators are cross-platform (`PathUtils.cs`).

## Forbidden

- Bypassing the placeholder/reference pipeline with ad-hoc
  `Regex.Replace` on raw YAML.
- Catching everything inside placeholder resolution and returning a
  successful result — surface failures.
- Skipping circular-reference detection in any placeholder change.
- Emitting empty `List<ValidationResult>` — return `null`.
- Tight coupling to specific YAML libraries from public surface.

## Tests

```bash
dotnet test QaaS.Framework.Configurations.Tests/QaaS.Framework.Configurations.Tests.csproj --nologo
```

See `QaaS.Framework.Configurations.Tests/CLAUDE.md`.
