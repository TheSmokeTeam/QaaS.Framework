using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Framework.Configurations.Tests;

[TestFixture]
public class CustomValidationAttributesBehaviorTests
{
    private sealed class PathContainer
    {
        [ValidPath]
        public string? Path { get; set; }
    }

    private sealed class PathListContainer
    {
        [AllPathsInEnumerableValid]
        public List<string> Paths { get; set; } = [];
    }

    private sealed class NullablePathListContainer
    {
        [AllPathsInEnumerableValid]
        public List<string?> Paths { get; set; } = [];
    }

    private sealed class UniqueItemsContainer
    {
        [UniqueItemsInEnumerable]
        public List<string> Items { get; set; } = [];
    }

    private sealed class PayloadWithFields
    {
        public string? First { get; set; }
        public string? Second { get; set; }
    }

    private sealed class AtLeastOnePropertyContainer
    {
        [AtLeastOnePropertyNotNull(nameof(PayloadWithFields.First), nameof(PayloadWithFields.Second))]
        public PayloadWithFields Payload { get; set; } = new();
    }

    private sealed class AtLeastOneEnumerableContainer
    {
        [AtLeastOneEnumerablePropertyNotEmpty(nameof(First), nameof(Second))]
        public AtLeastOneEnumerableContainer Self => this;

        public List<int> First { get; set; } = [];
        public List<int> Second { get; set; } = [];
    }

    [Test]
    public void ValidPathAttribute_PassesForValidPathAndFailsForInvalidPath()
    {
        var valid = new PathContainer { Path = "C:\\temp\\file.txt" };
        var invalid = new PathContainer { Path = "C:\\temp\\a\0b.txt" };

        Assert.That(Validator.TryValidateObject(valid, new ValidationContext(valid), null, true), Is.True);
        Assert.That(Validator.TryValidateObject(invalid, new ValidationContext(invalid), null, true), Is.False);
    }

    [Test]
    public void AllPathsInEnumerableValidAttribute_ValidatesAllItems()
    {
        var valid = new PathListContainer { Paths = ["C:\\temp\\a.txt", "C:\\temp\\b.txt"] };
        var invalid = new PathListContainer { Paths = ["C:\\temp\\a.txt", "C:\\temp\\a\0b.txt"] };
        var invalidNullEntry = new NullablePathListContainer { Paths = ["C:\\temp\\a.txt", null] };

        Assert.That(Validator.TryValidateObject(valid, new ValidationContext(valid), null, true), Is.True);
        Assert.That(Validator.TryValidateObject(invalid, new ValidationContext(invalid), null, true), Is.False);
        Assert.That(Validator.TryValidateObject(invalidNullEntry, new ValidationContext(invalidNullEntry), null, true),
            Is.False);
    }

    [Test]
    public void UniqueItemsInEnumerableAttribute_DetectsDuplicates()
    {
        var valid = new UniqueItemsContainer { Items = ["a", "b", "c"] };
        var invalid = new UniqueItemsContainer { Items = ["a", "b", "a"] };

        Assert.That(Validator.TryValidateObject(valid, new ValidationContext(valid), null, true), Is.True);
        Assert.That(Validator.TryValidateObject(invalid, new ValidationContext(invalid), null, true), Is.False);
    }

    [Test]
    public void AtLeastOnePropertyNotNullAttribute_RequiresAtLeastOneValue()
    {
        var valid = new AtLeastOnePropertyContainer
        {
            Payload = new PayloadWithFields { First = "x", Second = null }
        };
        var invalid = new AtLeastOnePropertyContainer
        {
            Payload = new PayloadWithFields { First = null, Second = null }
        };

        Assert.That(Validator.TryValidateObject(valid, new ValidationContext(valid), null, true), Is.True);
        Assert.That(Validator.TryValidateObject(invalid, new ValidationContext(invalid), null, true), Is.False);
    }

    [Test]
    public void AtLeastOneEnumerablePropertyNotEmptyAttribute_RequiresAnyNonEmptyEnumerable()
    {
        var valid = new AtLeastOneEnumerableContainer
        {
            First = [1],
            Second = []
        };
        var invalid = new AtLeastOneEnumerableContainer
        {
            First = [],
            Second = []
        };

        Assert.That(Validator.TryValidateObject(valid, new ValidationContext(valid), null, true), Is.True);
        Assert.That(Validator.TryValidateObject(invalid, new ValidationContext(invalid), null, true), Is.False);
    }
}
