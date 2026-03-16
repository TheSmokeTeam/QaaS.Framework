using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Configurations.CommonConfigurationObjects;
using QaaS.Framework.Configurations.ConfigurationBindingUtils;
using QaaS.Framework.Configurations.CustomExceptions;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Framework.Configurations.Tests;

[TestFixture]
public class ConfigurationCoverageEdgeCaseTests
{
    private enum BindingMode
    {
        First,
        Second
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => Scope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class Scope : IDisposable
        {
            public static Scope Instance { get; } = new();
            public void Dispose()
            {
            }
        }
    }

    private sealed class PrivateSetterSettings
    {
        public string? Name { get; private set; }
    }

    private sealed class CompositeBindingSettings
    {
        public Dictionary<int, string> NumericMap { get; set; } = [];
        public KeyValuePair<int, string> Pair { get; set; }
        public int[] Values { get; set; } = [];
        public IConfiguration? Section { get; set; }
        public PrivateSetterSettings Private { get; private set; } = new();
        public string Writable { get; set; } = string.Empty;
        public string ReadOnly => "computed";
    }

    private sealed class NonGenericDictionaryHolder
    {
        public Hashtable Map { get; set; } = new()
        {
            ["alpha"] = "one"
        };

        public string Name { get; set; } = "holder";

        public string this[int index] => $"idx-{index}";
    }

    private sealed class NonGenericDictionaryListHolder
    {
        public ArrayList Items { get; set; } =
        [
            new Hashtable
            {
                ["nested"] = "value"
            }
        ];
    }

    private abstract class BaseSettings
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class CurrentSettings : BaseSettings;
    private sealed class ReplacementSettings : BaseSettings;

    private sealed class NoDefaultCtorMergeSettings(string required)
    {
        public string Required { get; set; } = required;
        public List<int> Values { get; set; } = [];
        public Hashtable Extras { get; set; } = new();
    }

    private sealed class AnyPropertyPayload
    {
        public string? Left { get; set; }
        public string? Right { get; set; }
    }

    private sealed class AnyPropertyContainer
    {
        [AtLeastOnePropertyNotNull]
        public AnyPropertyPayload? Payload { get; set; }
    }

    private sealed class AnyEnumerablePayload
    {
        public List<int> Left { get; set; } = [];
        public List<int> Right { get; set; } = [];
    }

    private sealed class AnyEnumerableContainer
    {
        [AtLeastOneEnumerablePropertyNotEmpty]
        public AnyEnumerablePayload? Payload { get; set; }
    }

    private sealed class StringConditionAttribute : ConditionalValidationAttribute
    {
        public StringConditionAttribute() : base("Mode:Enabled,State:Open")
        {
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
            => CheckIfAnyConditionIsMet(validationContext)
                ? ValidationResult.Success
                : new ValidationResult("conditions not met");
    }

    private sealed class StringConditionContainer
    {
        public string? Mode { get; set; }
        public string? State { get; set; }

        [StringCondition]
        public string? Target { get; set; }
    }

    private sealed class ComparisonContainer
    {
        public string? Primary { get; set; }

        [EnumerablePropertyDoesNotContainAnotherPropertyValue(nameof(Primary))]
        public List<string?> Values { get; set; } = [];
    }

    private sealed class MissingComparisonContainer
    {
        [EnumerablePropertyDoesNotContainAnotherPropertyValue("Missing")]
        public List<string?> Values { get; set; } = [];
    }

    private static (bool IsValid, List<ValidationResult> Results) Validate(object value)
    {
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(value, new ValidationContext(value), results, true);
        return (isValid, results);
    }

    [Test]
    public void GetInMemoryCollectionFromObject_PreservesNonGenericDictionaryEntries_AndSkipsIndexers()
    {
        var flat = ConfigurationUtils.GetInMemoryCollectionFromObject(new NonGenericDictionaryHolder());

        Assert.Multiple(() =>
        {
            Assert.That(flat["Map:alpha"], Is.EqualTo("one"));
            Assert.That(flat["Name"], Is.EqualTo("holder"));
            Assert.That(flat.Keys.Any(key => key.StartsWith("Item", StringComparison.Ordinal)), Is.False);
        });
    }

    [Test]
    public void GetInMemoryCollectionFromObject_PreservesNonGenericDictionaryEntriesInsideLists()
    {
        var flat = ConfigurationUtils.GetInMemoryCollectionFromObject(new NonGenericDictionaryListHolder());

        Assert.That(flat["Items:0:nested"], Is.EqualTo("value"));
    }

    [Test]
    public void BindToObject_BindsDictionaryKeyValuePairPrivateSetter_AndLogsWarnings()
    {
        var logger = new RecordingLogger();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NumericMap:1"] = "one",
                ["NumericMap:2"] = "two",
                ["Pair:7"] = "seven",
                ["Values:0"] = "4",
                ["Values:1"] = "5",
                ["Section:child"] = "value",
                ["Private:Name"] = "hidden",
                ["Writable"] = "updated",
                ["ReadOnly"] = "ignored",
                ["Unknown"] = "extra"
            })
            .Build();

        var bound = configuration.BindToObject<CompositeBindingSettings>(new BinderOptions
        {
            ErrorOnUnknownConfiguration = true,
            BindNonPublicProperties = true
        }, logger);

        Assert.Multiple(() =>
        {
            Assert.That(bound.NumericMap[1], Is.EqualTo("one"));
            Assert.That(bound.Pair, Is.EqualTo(new KeyValuePair<int, string>(7, "seven")));
            Assert.That(bound.Values, Is.EqualTo(new[] { 4, 5 }));
            Assert.That(bound.Section?["child"], Is.EqualTo("value"));
            Assert.That(bound.Private.Name, Is.EqualTo("hidden"));
            Assert.That(bound.Writable, Is.EqualTo("updated"));
            Assert.That(logger.Entries.Any(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Unknown")), Is.True);
            Assert.That(logger.Entries.Any(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("ReadOnly")), Is.True);
        });
    }

    [Test]
    public void BindToObject_BindsScalarRootValues()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [string.Empty] = "7"
            })
            .Build();

        var scalar = configuration.BindToObject(typeof(int), new BinderOptions
        {
            ErrorOnUnknownConfiguration = true,
            BindNonPublicProperties = false
        });
        var rootConfiguration = (IConfiguration)configuration.BindToObject(typeof(IConfiguration), new BinderOptions
        {
            ErrorOnUnknownConfiguration = true,
            BindNonPublicProperties = false
        });

        Assert.Multiple(() =>
        {
            Assert.That(scalar, Is.EqualTo(7));
            Assert.That(rootConfiguration[string.Empty], Is.EqualTo("7"));
        });
    }

    [Test]
    public void MergeConfigurationObjectIntoIConfiguration_PreservesExistingCollections_WhenPatchHasNoDefaultsConstructorAndEmptyCollections()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Required"] = "current",
                ["Values:0"] = "1",
                ["Values:1"] = "2",
                ["Extras:left"] = "value"
            })
            .Build();
        var patch = new NoDefaultCtorMergeSettings("patched")
        {
            Values = [],
            Extras = new Hashtable()
        };

        var merged = configuration.MergeConfigurationObjectIntoIConfiguration(patch);

        Assert.Multiple(() =>
        {
            Assert.That(merged["Required"], Is.EqualTo("patched"));
            Assert.That(merged["Values:0"], Is.EqualTo("1"));
            Assert.That(merged["Values:1"], Is.EqualTo("2"));
            Assert.That(merged["Extras:left"], Is.EqualTo("value"));
        });
    }

    [Test]
    public void MergeConfiguration_WhenRuntimeTypesDiffer_ReturnsReplacementConfiguration()
    {
        BaseSettings current = new CurrentSettings { Name = "current" };
        BaseSettings replacement = new ReplacementSettings { Name = "replacement" };

        var merged = current.MergeConfiguration(replacement);

        Assert.That(merged, Is.SameAs(replacement));
    }

    [Test]
    public void MergeConfiguration_WhenPatchIsNull_PreservesCurrentConfiguration()
    {
        BaseSettings current = new CurrentSettings { Name = "current" };

        var merged = current.MergeConfiguration<BaseSettings>(null);

        Assert.That(merged, Is.SameAs(current));
    }

    [Test]
    public void InternalDictionaryAndTypeUtilities_HandleHelpersAndInvalidEnumConversion()
    {
        var logger = new RecordingLogger();
        var assembly = typeof(ConfigurationUtils).Assembly;
        var dictionaryUtilsType = assembly.GetType("QaaS.Framework.Configurations.ConfigurationBindingUtils.DictionaryUtils", true)!;
        var typeUtilsType = assembly.GetType("QaaS.Framework.Configurations.ConfigurationBindingUtils.TypeUtils", true)!;

        var numericList = (IList)dictionaryUtilsType
            .GetMethod("ConvertDictionaryToListAccordingToKeys", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
            .Invoke(null, [new Dictionary<string, object> { ["1"] = "second", ["0"] = "first" }])!;
        var stringObjectDictionary = (Dictionary<string, object?>)dictionaryUtilsType
            .GetMethod("ToStringObjectDictionary", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
            .Invoke(null, [new Hashtable { ["answer"] = 42 }])!;
        var isDictionary = (bool)dictionaryUtilsType
            .GetMethod("IsTypeDictionary", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
            .Invoke(null, [typeof(Dictionary<string, string>)])!;
        var isKeyValuePair = (bool)dictionaryUtilsType
            .GetMethod("IsTypeKeyValuePair", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
            .Invoke(null, [typeof(KeyValuePair<int, string>)])!;

        var configurationInstance = typeUtilsType
            .GetMethod("CreateInstance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
            .Invoke(null, [typeof(IConfiguration)]);
        var unsupportedInterfaceInstance = typeUtilsType
            .GetMethod("CreateInstance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
            .Invoke(null, [typeof(IDisposable)]);
        var invalidEnumValue = typeUtilsType
            .GetMethod("ConvertSimpleValueToType", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
            .Invoke(null, [typeof(BindingMode), "missing", logger, ":root:mode"]);
        var parentPath = (string)typeUtilsType
            .GetMethod("GetParentPathPrefix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
            .Invoke(null, [":root:mode"])!;
        var defaultValue = typeUtilsType
            .GetMethod("GetDefaultValue", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
            .Invoke(null, [typeof(int)]);

        Assert.Multiple(() =>
        {
            Assert.That(numericList.Cast<object>(), Is.EqualTo(new[] { "first", "second" }));
            Assert.That(stringObjectDictionary["answer"], Is.EqualTo(42));
            Assert.That(isDictionary, Is.True);
            Assert.That(isKeyValuePair, Is.True);
            Assert.That(configurationInstance, Is.InstanceOf<IConfiguration>());
            Assert.That(unsupportedInterfaceInstance, Is.Null);
            Assert.That(invalidEnumValue, Is.Null);
            Assert.That(parentPath, Is.EqualTo("root:mode -"));
            Assert.That(defaultValue, Is.EqualTo(0));
            Assert.That(logger.Entries.Any(entry => entry.Level == LogLevel.Warning), Is.True);
        });
    }

    [Test]
    public void ValidationAttributes_DefaultAndStringConditionPaths_AreCovered()
    {
        var missingValues = Validate(new AnyPropertyContainer { Payload = new AnyPropertyPayload() });
        var validProperty = Validate(new AnyPropertyContainer
        {
            Payload = new AnyPropertyPayload { Left = "value" }
        });
        var missingEnumerableValues = Validate(new AnyEnumerableContainer
        {
            Payload = new AnyEnumerablePayload()
        });
        var validEnumerable = Validate(new AnyEnumerableContainer
        {
            Payload = new AnyEnumerablePayload { Right = [1] }
        });
        var stringConditionValid = Validate(new StringConditionContainer { Mode = "Enabled", State = "Open" });
        var stringConditionInvalid = Validate(new StringConditionContainer { Mode = "Disabled", State = "Closed" });
        var comparisonInvalid = Validate(new ComparisonContainer { Primary = "dup", Values = ["dup"] });
        var comparisonValid = Validate(new ComparisonContainer { Primary = "dup", Values = ["other"] });

        Assert.Multiple(() =>
        {
            Assert.That(missingValues.IsValid, Is.False);
            Assert.That(validProperty.IsValid, Is.True);
            Assert.That(missingEnumerableValues.IsValid, Is.False);
            Assert.That(validEnumerable.IsValid, Is.True);
            Assert.That(stringConditionValid.IsValid, Is.True);
            Assert.That(stringConditionInvalid.IsValid, Is.False);
            Assert.That(comparisonInvalid.IsValid, Is.False);
            Assert.That(comparisonValid.IsValid, Is.True);
            Assert.Throws<ArgumentException>(() => Validate(new MissingComparisonContainer { Values = ["x"] }));
        });
    }

    [Test]
    public void FilesInFileSystemConfig_And_InvalidConfigurationsException_ExposeDefaults()
    {
        var config = new FilesInFileSystemConfig { Path = "C:\\temp" };
        var exception = new InvalidConfigurationsException("invalid");

        Assert.Multiple(() =>
        {
            Assert.That(config.Path, Is.EqualTo("C:\\temp"));
            Assert.That(config.SearchPattern, Is.EqualTo(string.Empty));
            Assert.That(exception.Message, Is.EqualTo("invalid"));
            Assert.That(exception.StackTrace, Is.EqualTo(string.Empty));
        });
    }
}
