# CLAUDE.md — QaaS.Framework.Configurations.Tests

## Purpose

Test project for `QaaS.Framework.Configurations`. Covers placeholder
resolution (including circular-reference detection), reference
expansion, every custom validation attribute, configuration update
merging, and the HTTP YAML provider.

## Layout

- `ConfigurationObjectValidationTests.cs` — DataAnnotations-driven
  validation against representative DTOs.
- `CustomValidationAttributesBehaviorTests.cs` — happy-path coverage for
  each attribute in `CustomValidationAttributes/`.
- `AdvancedCustomValidationAttributesTests.cs` — interaction between
  multiple attributes.
- `ValueAppearsInListAttributeTests.cs` — focused fixture for the most
  common list-membership attribute.
- `ConfigurationUtilitiesTests.cs` — `ConfigurationUtils` /
  `PathUtils` behaviour.
- `ConfigurationUpdateExtensionsTests.cs` — update-merge normalisation.
- `ConfigurationCoverageEdgeCaseTests.cs`,
  `ConfigurationInternalHelpersCoverageTests.cs` — defensive paths.
- `HttpGetYamlConfigurationProviderTests.cs` — HTTP source provider.
- `GlobalUsings.cs` — `global using NUnit.Framework;`.

## Conventions

- **NUnit 4** + **Moq**. Constraint-model assertions
  (`Assert.That(...)`).
- Cycle / failure paths assert exception type and message substring.
- HTTP provider tests use `Moq` `HttpMessageHandler` doubles — never
  hit real endpoints.
- Validation tests construct minimal anonymous-style DTOs locally
  rather than reusing production DTOs where possible, so the test owns
  its shape.
- `null` validation result means "no errors"; assertions reflect that.

## Forbidden

- Network I/O in HTTP-provider tests.
- Sharing mutable `ConfigurationBuilder` instances across tests.
- Asserting on raw stack traces.
- Re-implementing the placeholder regex inside the test — drive through
  the real parser.

## Run

```bash
dotnet test QaaS.Framework.Configurations.Tests/QaaS.Framework.Configurations.Tests.csproj --nologo
```
