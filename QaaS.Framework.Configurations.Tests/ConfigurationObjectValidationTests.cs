using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CommonConfigurationObjects;
using QaaS.Framework.Configurations.CustomValidationAttributes;
using QaaS.Framework.Configurations.References;

namespace QaaS.Framework.Configurations.Tests;

[TestFixture]
public class ConfigurationObjectValidationTests
{
    [PropertyComparison(nameof(Min), nameof(Max), PropertyComparisonOperator.LessThanOrEqual)]
    private sealed class ComparisonSample
    {
        public int? Min { get; set; }
        public int? Max { get; set; }
    }

    [PropertyComparison("Missing", nameof(Max), PropertyComparisonOperator.LessThanOrEqual)]
    private sealed class MissingComparisonPropertySample
    {
        public int? Max { get; set; }
    }

    private sealed class ConfiguredStateSample
    {
        public Dictionary<string, string> Claims { get; set; } = new();

        [RequiredOrNullBasedOnOtherFieldsConfiguration(new[] { nameof(Claims) }, false)]
        public string? HierarchicalClaims { get; set; }
    }

    private sealed class YamlPayloadSample
    {
        [YamlStringDeserializable(typeof(Dictionary<string, object>))]
        public string? Payload { get; set; }
    }

    private static (bool IsValid, List<ValidationResult> Results) Validate(object value)
    {
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(value, new ValidationContext(value), results, true);
        return (isValid, results);
    }

    [Test]
    public void PropertyComparisonAttribute_ValidatesComparableProperties()
    {
        var valid = Validate(new ComparisonSample { Min = 1, Max = 2 });
        var invalid = Validate(new ComparisonSample { Min = 3, Max = 2 });
        var ignoredWhenNull = Validate(new ComparisonSample { Min = null, Max = 2 });

        Assert.Multiple(() =>
        {
            Assert.That(valid.IsValid, Is.True);
            Assert.That(invalid.IsValid, Is.False);
            Assert.That(invalid.Results.Single().ErrorMessage, Does.Contain("less than or equal to"));
            Assert.That(ignoredWhenNull.IsValid, Is.True);
            Assert.Throws<ArgumentException>(() => Validate(new MissingComparisonPropertySample { Max = 1 }));
        });
    }

    [Test]
    public void RequiredOrNullBasedOnOtherFieldsConfiguration_TreatsEmptyCollectionsAsNotConfigured()
    {
        var validWhenOtherIsEmpty = Validate(new ConfiguredStateSample
        {
            Claims = [],
            HierarchicalClaims = "sub: 1"
        });
        var invalidWhenOtherIsConfigured = Validate(new ConfiguredStateSample
        {
            Claims = new Dictionary<string, string> { ["sub"] = "1" },
            HierarchicalClaims = "sub: 1"
        });

        Assert.Multiple(() =>
        {
            Assert.That(validWhenOtherIsEmpty.IsValid, Is.True);
            Assert.That(invalidWhenOtherIsConfigured.IsValid, Is.False);
            Assert.That(invalidWhenOtherIsConfigured.Results.Single().ErrorMessage, Does.Contain("must be empty"));
        });
    }

    [Test]
    public void YamlStringDeserializableAttribute_RejectsInvalidYaml()
    {
        var valid = Validate(new YamlPayloadSample { Payload = "sub: 1" });
        var invalid = Validate(new YamlPayloadSample { Payload = "[bad" });

        Assert.Multiple(() =>
        {
            Assert.That(valid.IsValid, Is.True);
            Assert.That(invalid.IsValid, Is.False);
            Assert.That(invalid.Results.Single().ErrorMessage, Does.Contain("valid YAML"));
        });
    }

    [Test]
    public void ReferenceConfig_RequiresUniqueValidReferenceFiles()
    {
        var invalidPathCharacter = Path.GetInvalidPathChars().First();
        var valid = Validate(new ReferenceConfig
        {
            ReferenceReplaceKeyword = "__REF__",
            ReferenceFilesPaths = ["C:\\temp\\ref.yaml"]
        });
        var duplicate = Validate(new ReferenceConfig
        {
            ReferenceReplaceKeyword = "__REF__",
            ReferenceFilesPaths = ["C:\\temp\\ref.yaml", "C:\\temp\\ref.yaml"]
        });
        var missingPaths = Validate(new ReferenceConfig
        {
            ReferenceReplaceKeyword = "__REF__",
            ReferenceFilesPaths = []
        });
        var invalidPath = Validate(new ReferenceConfig
        {
            ReferenceReplaceKeyword = "__REF__",
            ReferenceFilesPaths = [$"C:\\temp\\bad{invalidPathCharacter}.yaml"]
        });

        Assert.Multiple(() =>
        {
            Assert.That(valid.IsValid, Is.True);
            Assert.That(duplicate.IsValid, Is.False);
            Assert.That(missingPaths.IsValid, Is.False);
            Assert.That(invalidPath.IsValid, Is.False);
        });
    }

    [Test]
    public void FilesInFileSystemConfig_RequiresValidPath()
    {
        var invalidPathCharacter = Path.GetInvalidPathChars().First();
        var valid = Validate(new FilesInFileSystemConfig { Path = "C:\\temp" });
        var invalid = Validate(new FilesInFileSystemConfig { Path = $"C:\\temp\\bad{invalidPathCharacter}" });

        Assert.Multiple(() =>
        {
            Assert.That(valid.IsValid, Is.True);
            Assert.That(invalid.IsValid, Is.False);
        });
    }
}
