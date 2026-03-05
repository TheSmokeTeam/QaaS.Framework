using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Framework.Configurations.Tests;

[TestFixture]
public class AdvancedCustomValidationAttributesTests
{
    private sealed class ConditionalSample
    {
        public string? Mode { get; set; }

        [RequiredIfAny(nameof(Mode), "Enabled", "Active")]
        public string? RequiredValue { get; set; }

        [NullIfAny(nameof(Mode), "Disabled")]
        public string? MustBeNullWhenDisabled { get; set; }
    }

    private sealed class MissingConditionPropertySample
    {
        [RequiredIfAny("MissingProperty:1")]
        public string? Value { get; set; }
    }

    private sealed class NullUnlessSample
    {
        public string? A { get; set; }
        public string? B { get; set; }

        [NullUnlessAll(new[] { nameof(A), nameof(B) }, "x", "y")]
        public string? NullableValue { get; set; }
    }

    private sealed class RequiredUnlessSample
    {
        public string? A { get; set; }
        public string? B { get; set; }

        [RequiredUnlessAll(new[] { nameof(A), nameof(B) }, "x", "y")]
        public string? RequiredValue { get; set; }
    }

    private sealed class RangeIfAnySample
    {
        public string? Tier { get; set; }

        [RangeIfAny(nameof(Tier), new object[] { "Basic", "Pro" }, new[] { 1, 10 }, new[] { 5, 20 })]
        public object? Amount { get; set; }
    }

    private sealed class RangeIfAnyMissingFieldSample
    {
        [RangeIfAny("Missing", new object[] { "A" }, new[] { 1 }, new[] { 2 })]
        public int Amount { get; set; }
    }

    private sealed class NullabilityPayload
    {
        public string? P1 { get; set; }
        public string? P2 { get; set; }
        public string? P3 { get; set; }
    }

    private sealed class NoMoreThanAllSample
    {
        [NoMoreThanXPropertiesNotNull(2)]
        public NullabilityPayload? Payload { get; set; }
    }

    private sealed class NoMoreThanSpecificSample
    {
        [NoMoreThanXPropertiesNotNull(new[] { nameof(NullabilityPayload.P1), nameof(NullabilityPayload.P2) }, 1)]
        public NullabilityPayload? Payload { get; set; }
    }

    private sealed class UniqueItem
    {
        public string? Id { get; set; }
    }

    private sealed class UniqueEnumerableSample
    {
        [UniquePropertyInEnumerable(nameof(UniqueItem.Id))]
        public List<UniqueItem> Items { get; set; } = [];
    }

    private sealed class UniqueEnumerableWrongFieldSample
    {
        [UniquePropertyInEnumerable("MissingField")]
        public List<UniqueItem> Items { get; set; } = [];
    }

    private sealed class GraphNode
    {
        public string? Name { get; set; }
        public List<string?> DependsOn { get; set; } = [];
    }

    private sealed class GraphSample
    {
        [AllItemsInEnumerablePropertyInEnumerableExistAsPropertyInEnumerable(
            nameof(GraphNode.DependsOn), nameof(GraphNode.Name))]
        public List<GraphNode> Nodes { get; set; } = [];
    }

    private sealed class BadGraphNode
    {
        public string? Name { get; set; }
        public int DependsOn { get; set; }
    }

    private sealed class BadGraphSample
    {
        [AllItemsInEnumerablePropertyInEnumerableExistAsPropertyInEnumerable(
            nameof(BadGraphNode.DependsOn), nameof(BadGraphNode.Name))]
        public List<BadGraphNode> Nodes { get; set; } = [];
    }

    private sealed class DualListHolder
    {
        public List<UniqueItem>? Left { get; set; }
        public List<UniqueItem>? Right { get; set; }
    }

    private sealed class UniqueAcrossPropertiesSample
    {
        [UniquePropertyInEnumerableProperties(nameof(UniqueItem.Id), "id collisions are not allowed",
            nameof(DualListHolder.Left), nameof(DualListHolder.Right))]
        public DualListHolder? Holder { get; set; }
    }

    private sealed class UniqueAcrossPropertiesListSample
    {
        [UniquePropertyInEnumerableProperties(nameof(UniqueItem.Id), "id collisions are not allowed",
            nameof(DualListHolder.Left), nameof(DualListHolder.Right))]
        public List<DualListHolder> Holders { get; set; } = [];
    }

    private sealed class UniqueAcrossPropertiesMissingEnumerableSample
    {
        [UniquePropertyInEnumerableProperties(nameof(UniqueItem.Id), "id collisions are not allowed", "Missing")]
        public DualListHolder? Holder { get; set; }
    }

    private sealed class RequiredOrNullSample
    {
        public string? TriggerRequired { get; set; }
        public string? TriggerMustBeNull { get; set; }

        [RequiredOrNullBasedOnOtherFieldsConfiguration(
            new[] { nameof(TriggerRequired), nameof(TriggerMustBeNull) }, true, false)]
        public string? Target { get; set; }
    }

    private sealed class RequiredOrNullMissingPropertySample
    {
        [RequiredOrNullBasedOnOtherFieldsConfiguration(new[] { "MissingProperty" }, true)]
        public string? Target { get; set; }
    }

    private static (bool IsValid, List<ValidationResult> ValidationResults) Validate(object value)
    {
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(value, new ValidationContext(value), validationResults, true);
        return (isValid, validationResults);
    }

    [Test]
    public void RequiredIfAny_And_NullIfAny_ValidateConditionalBranches()
    {
        var requiredFailure = new ConditionalSample { Mode = "Enabled", RequiredValue = null, MustBeNullWhenDisabled = "v" };
        var requiredResult = Validate(requiredFailure);
        Assert.That(requiredResult.IsValid, Is.False);
        Assert.That(requiredResult.ValidationResults.Any(result => result.ErrorMessage!.Contains("required")), Is.True);

        var nullIfAnyFailure = new ConditionalSample { Mode = "Disabled", RequiredValue = "ok", MustBeNullWhenDisabled = "x" };
        var nullIfAnyResult = Validate(nullIfAnyFailure);
        Assert.That(nullIfAnyResult.IsValid, Is.False);
        Assert.That(nullIfAnyResult.ValidationResults.Any(result => result.ErrorMessage!.Contains("required to be null")), Is.True);

        var success = new ConditionalSample { Mode = "Other", RequiredValue = null, MustBeNullWhenDisabled = "x" };
        Assert.That(Validate(success).IsValid, Is.True);
    }

    [Test]
    public void ConditionalValidation_ConstructorsAndMissingProperties_AreHandled()
    {
        Assert.Throws<NotSupportedException>(() => _ = new RequiredIfAnyAttribute(new[] { "A" }, "x", "y"));

        Assert.Throws<NotSupportedException>(() => Validate(new MissingConditionPropertySample { Value = null }));
    }

    [Test]
    public void NullUnlessAll_And_RequiredUnlessAll_ValidateAllBranches()
    {
        var nullUnlessValid = new NullUnlessSample { A = "x", B = "y", NullableValue = "value" };
        var nullUnlessInvalid = new NullUnlessSample { A = "x", B = "z", NullableValue = "value" };
        var nullUnlessNull = new NullUnlessSample { A = "x", B = "z", NullableValue = null };

        Assert.That(Validate(nullUnlessValid).IsValid, Is.True);
        Assert.That(Validate(nullUnlessInvalid).IsValid, Is.False);
        Assert.That(Validate(nullUnlessNull).IsValid, Is.True);

        var requiredUnlessValid = new RequiredUnlessSample { A = "x", B = "y", RequiredValue = null };
        var requiredUnlessInvalid = new RequiredUnlessSample { A = "x", B = "z", RequiredValue = null };
        var requiredUnlessHasValue = new RequiredUnlessSample { A = "x", B = "z", RequiredValue = "v" };

        Assert.That(Validate(requiredUnlessValid).IsValid, Is.True);
        Assert.That(Validate(requiredUnlessInvalid).IsValid, Is.False);
        Assert.That(Validate(requiredUnlessHasValue).IsValid, Is.True);
    }

    [Test]
    public void NullUnlessAll_And_RequiredUnlessAll_Constructors_ValidateArguments()
    {
        Assert.Throws<NotSupportedException>(() => _ = new NullUnlessAllAttribute(new[] { "A" }, "x", "y"));
        Assert.Throws<NotSupportedException>(() => _ = new RequiredUnlessAllAttribute(new[] { "A" }, "x", "y"));
    }

    [Test]
    public void RangeIfAny_ValidatesRanges_ComparableTypes_AndFieldLookup()
    {
        var valid = Validate(new RangeIfAnySample { Tier = "Basic", Amount = 3 });
        var outOfRange = Validate(new RangeIfAnySample { Tier = "Basic", Amount = 8 });
        var notComparable = Validate(new RangeIfAnySample { Tier = "Basic", Amount = new object() });
        var nullValue = Validate(new RangeIfAnySample { Tier = "Basic", Amount = null });
        var missingField = Validate(new RangeIfAnyMissingFieldSample { Amount = 1 });
        var nullTier = Validate(new RangeIfAnySample { Tier = null, Amount = 2 });

        Assert.Multiple(() =>
        {
            Assert.That(valid.IsValid, Is.True);
            Assert.That(outOfRange.IsValid, Is.False);
            Assert.That(notComparable.IsValid, Is.False);
            Assert.That(nullValue.IsValid, Is.False);
            Assert.That(missingField.IsValid, Is.False);
            Assert.That(nullTier.IsValid, Is.False);
            Assert.That(outOfRange.ValidationResults.Single().ErrorMessage, Does.Contain("must be between"));
            Assert.That(notComparable.ValidationResults.Single().ErrorMessage, Does.Contain("comparable"));
            Assert.That(missingField.ValidationResults.Single().ErrorMessage, Does.Contain("Missing field"));
        });
    }

    [Test]
    public void RangeIfAny_Constructor_ThrowsOnMismatchedLengths()
    {
        Assert.Throws<ArgumentException>(() =>
            _ = new RangeIfAnyAttribute("Tier", new object[] { "A" }, new[] { 1, 2 }, new[] { 3 }));
    }

    [Test]
    public void NoMoreThanXPropertiesNotNull_ValidatesAllAndSpecificPropertyModes()
    {
        var allValid = Validate(new NoMoreThanAllSample
        {
            Payload = new NullabilityPayload { P1 = "1", P2 = "2", P3 = null }
        });
        var allInvalid = Validate(new NoMoreThanAllSample
        {
            Payload = new NullabilityPayload { P1 = "1", P2 = "2", P3 = "3" }
        });
        var nullPayload = Validate(new NoMoreThanAllSample { Payload = null });

        var specificValid = Validate(new NoMoreThanSpecificSample
        {
            Payload = new NullabilityPayload { P1 = "1", P2 = null, P3 = "3" }
        });
        var specificInvalid = Validate(new NoMoreThanSpecificSample
        {
            Payload = new NullabilityPayload { P1 = "1", P2 = "2", P3 = null }
        });

        Assert.Multiple(() =>
        {
            Assert.That(allValid.IsValid, Is.True);
            Assert.That(allInvalid.IsValid, Is.False);
            Assert.That(nullPayload.IsValid, Is.False);
            Assert.That(specificValid.IsValid, Is.True);
            Assert.That(specificInvalid.IsValid, Is.False);
        });
    }

    [Test]
    public void UniquePropertyInEnumerable_DetectsDuplicates_AndThrowsWhenPropertyMissing()
    {
        var valid = Validate(new UniqueEnumerableSample
        {
            Items = [new UniqueItem { Id = "a" }, new UniqueItem { Id = "b" }]
        });
        var invalid = Validate(new UniqueEnumerableSample
        {
            Items = [new UniqueItem { Id = "a" }, new UniqueItem { Id = "a" }]
        });

        Assert.That(valid.IsValid, Is.True);
        Assert.That(invalid.IsValid, Is.False);
        Assert.Throws<NotSupportedException>(() => Validate(new UniqueEnumerableWrongFieldSample
        {
            Items = [new UniqueItem { Id = "a" }]
        }));
    }

    [Test]
    public void AllItemsInEnumerablePropertyInEnumerableExistAsPropertyInEnumerable_ValidatesReferences()
    {
        var valid = Validate(new GraphSample
        {
            Nodes =
            [
                new GraphNode { Name = "A", DependsOn = [] },
                new GraphNode { Name = "B", DependsOn = ["A"] }
            ]
        });

        var invalid = Validate(new GraphSample
        {
            Nodes =
            [
                new GraphNode { Name = "A", DependsOn = [] },
                new GraphNode { Name = "B", DependsOn = ["Missing"] }
            ]
        });

        Assert.Multiple(() =>
        {
            Assert.That(valid.IsValid, Is.True);
            Assert.That(invalid.IsValid, Is.False);
            Assert.That(invalid.ValidationResults.Single().ErrorMessage, Does.Contain("contains an item not found"));
        });
    }

    [Test]
    public void AllItemsInEnumerablePropertyInEnumerableExistAsPropertyInEnumerable_ThrowsForNonEnumerableInnerField()
    {
        Assert.Throws<ArgumentException>(() => Validate(new BadGraphSample
        {
            Nodes = [new BadGraphNode { Name = "A", DependsOn = 1 }]
        }));
    }

    [Test]
    public void UniquePropertyInEnumerableProperties_ValidatesSingleObjectAndEnumerableModes()
    {
        var singleValid = Validate(new UniqueAcrossPropertiesSample
        {
            Holder = new DualListHolder
            {
                Left = [new UniqueItem { Id = "a" }],
                Right = [new UniqueItem { Id = "b" }]
            }
        });

        var singleInvalid = Validate(new UniqueAcrossPropertiesSample
        {
            Holder = new DualListHolder
            {
                Left = [new UniqueItem { Id = "a" }],
                Right = [new UniqueItem { Id = "a" }]
            }
        });

        var listInvalid = Validate(new UniqueAcrossPropertiesListSample
        {
            Holders =
            [
                new DualListHolder
                {
                    Left = [new UniqueItem { Id = "x" }],
                    Right = [new UniqueItem { Id = "y" }]
                },
                new DualListHolder
                {
                    Left = [new UniqueItem { Id = "k" }],
                    Right = [new UniqueItem { Id = "k" }]
                }
            ]
        });

        Assert.Multiple(() =>
        {
            Assert.That(singleValid.IsValid, Is.True);
            Assert.That(singleInvalid.IsValid, Is.False);
            Assert.That(listInvalid.IsValid, Is.False);
        });
    }

    [Test]
    public void UniquePropertyInEnumerableProperties_ThrowsWhenEnumerablePropertyMissing()
    {
        Assert.Throws<NotSupportedException>(() => Validate(new UniqueAcrossPropertiesMissingEnumerableSample
        {
            Holder = new DualListHolder
            {
                Left = [new UniqueItem { Id = "a" }],
                Right = [new UniqueItem { Id = "b" }]
            }
        }));
    }

    [Test]
    public void RequiredOrNullBasedOnOtherFieldsConfiguration_ValidatesRequiredAndNullRules()
    {
        var missingRequiredTarget = Validate(new RequiredOrNullSample
        {
            TriggerRequired = "configured",
            TriggerMustBeNull = null,
            Target = null
        });

        var targetMustBeNull = Validate(new RequiredOrNullSample
        {
            TriggerRequired = null,
            TriggerMustBeNull = "configured",
            Target = "value"
        });

        var valid = Validate(new RequiredOrNullSample
        {
            TriggerRequired = "configured",
            TriggerMustBeNull = null,
            Target = "value"
        });

        Assert.Multiple(() =>
        {
            Assert.That(missingRequiredTarget.IsValid, Is.False);
            Assert.That(targetMustBeNull.IsValid, Is.False);
            Assert.That(valid.IsValid, Is.True);
            Assert.That(missingRequiredTarget.ValidationResults.Single().ErrorMessage, Does.Contain("required"));
            Assert.That(targetMustBeNull.ValidationResults.Single().ErrorMessage, Does.Contain("must be empty"));
        });
    }

    [Test]
    public void RequiredOrNullBasedOnOtherFieldsConfiguration_ConstructorGuardsAndMissingProperty_AreValidated()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _ = new RequiredOrNullBasedOnOtherFieldsConfiguration(null!, true));
        Assert.Throws<ArgumentException>(() =>
            _ = new RequiredOrNullBasedOnOtherFieldsConfiguration(new[] { "A" }, true, false));
        Assert.Throws<ArgumentException>(() =>
            Validate(new RequiredOrNullMissingPropertySample { Target = "x" }));
    }
}
