# project_specs.md — QaaS.Framework.Configurations

YAML loading + placeholder & reference resolution + DataAnnotations
validation.

## Pipeline

1. **Load**: file / HTTP / embedded resource → string.
2. **Placeholder resolution** (`${section.path ?? default}`): iterative
   with circular-reference detection (`_resolutionStack`). Throws
   `InvalidOperationException` on cycles.
3. **Reference resolution**: a single keyword inside a list expands to
   referenced items; subsequent indices shift to maintain order.
4. **Validation**: standard DataAnnotations + ~20 custom attributes.
5. **Bind**: deserialise to C# object graph.

## Custom validation attributes (selection)

Dependency: `RequiredIfAny`, `NullIfAny`, `RequiredUnlessAll`.
Collection: `UniquePropertyInEnumerable`, …`AllItemsExist…`.
List: `ValueAppearsInList`, …`DoesNotContainAnotherPropertyValue`.
Path: `ValidPath`, `AllPathsInEnumerableValid`.
Comparison: `PropertyComparison(other, op)`.
Conditional: `ConditionalValidation`, `YamlStringDeserializable`.

## Forbidden in this project

- Bypassing the placeholder/reference pipeline (e.g. ad-hoc
  `Regex.Replace` on YAML strings).
- Returning empty validation lists — `null` means "no errors".
- Catching everything inside placeholder resolution and turning it into
  a successful result; surface failures.

## Tests

`QaaS.Framework.Configurations.Tests` — resolution, reference expansion,
validation attribute behaviour, edge cases (cycles, missing keys).
