using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Configurations.ConfigurationBindingUtils;

namespace QaaS.Framework.Configurations.Tests;

[TestFixture]
public class ConfigurationInternalHelpersCoverageTests
{
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

    private enum MergeMode
    {
        One,
        Two
    }

    private sealed class SampleChild
    {
        public string? Name { get; set; }
    }

    private sealed class SampleContainer
    {
        public IConfiguration? Config { get; set; }
        public Hashtable Map { get; set; } = new();
        public List<object?> Items { get; set; } = [];
        public SampleChild Child { get; set; } = new();
        public string? Text { get; set; }
    }

    private sealed class GetterShapes
    {
        public string Auto { get; set; } = "auto";
        public string Computed => Auto.ToUpperInvariant();
        public string Broken => throw new InvalidOperationException("boom");
    }

    private sealed class NullableScalarHolder
    {
        public string? Maybe { get; set; }
    }

    private sealed class NoDefaultCtorSample
    {
        public NoDefaultCtorSample(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }

    private sealed class GetOnlyAutoHolder
    {
        public string Value { get; } = "auto";
    }

    private static readonly Type MergeUtilsType = typeof(ConfigurationUtils).Assembly
        .GetType("QaaS.Framework.Configurations.ConfigurationBindingUtils.ConfigurationMergeUtils", true)!;
    private static readonly Type DictionaryUtilsType = typeof(ConfigurationUtils).Assembly
        .GetType("QaaS.Framework.Configurations.ConfigurationBindingUtils.DictionaryUtils", true)!;

    private static MethodInfo GetMergeMethod(string methodName) => MergeUtilsType.GetMethod(
        methodName,
        BindingFlags.Static | BindingFlags.NonPublic)!;

    private static object? InvokeMerge(string methodName, params object?[] args)
        => GetMergeMethod(methodName).Invoke(null, args);

    private static object? InvokeDictionaryUtils(string methodName, params object?[] args) => DictionaryUtilsType
        .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
        .Invoke(null, args);

    [Test]
    public void MergeUtility_PrivateMergeBranches_HandleDictionariesListsNullAndScalars()
    {
        var mergedDictionary = (Dictionary<string, object?>)InvokeMerge("MergeValues",
            new Dictionary<string, object?> { ["left"] = 1 },
            new Dictionary<string, object?> { ["right"] = 2 })!;
        var preservedList = (IList)InvokeMerge("MergeValues",
            new List<object?> { 1, 2 },
            new List<object?>())!;
        var replacedList = (IList)InvokeMerge("MergeValues",
            new List<object?> { 1 },
            new List<object?> { 3, 4 })!;
        var clonedScalar = InvokeMerge("MergeValues", "current", null);
        var replacedScalar = InvokeMerge("MergeValues", "current", "patch");

        Assert.Multiple(() =>
        {
            Assert.That(mergedDictionary["left"], Is.EqualTo(1));
            Assert.That(mergedDictionary["right"], Is.EqualTo(2));
            Assert.That(preservedList.Cast<object?>(), Is.EqualTo(new object?[] { 1, 2 }));
            Assert.That(preservedList, Is.Not.SameAs(new List<object?> { 1, 2 }));
            Assert.That(replacedList.Cast<object?>(), Is.EqualTo(new object?[] { 3, 4 }));
            Assert.That(clonedScalar, Is.EqualTo("current"));
            Assert.That(replacedScalar, Is.EqualTo("patch"));
        });
    }

    [Test]
    public void MergeUtility_PrivateConversionAndSkipBranches_HandleConfigurationsCollectionsAndObjects()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["nested:value"] = "1"
            })
            .Build();
        var container = new SampleContainer
        {
            Config = config,
            Map = new Hashtable
            {
                ["alpha"] = "one"
            },
            Items = [1, new SampleChild { Name = "child" }],
            Child = new SampleChild { Name = "node" },
            Text = "plain"
        };

        var patchConfig = (Dictionary<string, object?>)InvokeMerge("ConvertPatchValue", typeof(IConfiguration), config)!;
        var patchDictionary = (Dictionary<string, object?>)InvokeMerge("ConvertPatchValue", typeof(Hashtable), container.Map)!;
        var patchList = (List<object?>)InvokeMerge("ConvertPatchValue", typeof(List<object?>), container.Items)!;
        var patchClass = (Dictionary<string, object?>)InvokeMerge("ConvertPatchValue", typeof(SampleChild), container.Child)!;

        var currentConfig = (Dictionary<string, object?>)InvokeMerge("ConvertCurrentValue", typeof(IConfiguration), config)!;
        var currentDictionary = (Dictionary<string, object?>)InvokeMerge("ConvertCurrentValue", typeof(Hashtable), container.Map)!;
        var currentList = (List<object?>)InvokeMerge("ConvertCurrentValue", typeof(List<object?>), container.Items)!;
        var currentClass = (Dictionary<string, object?>)InvokeMerge("ConvertCurrentValue", typeof(SampleChild), container.Child)!;

        var shouldSkipConfig = (bool)InvokeMerge("ShouldSkipPatchValue", typeof(IConfiguration), config, config)!;
        var shouldSkipDictionary = (bool)InvokeMerge("ShouldSkipPatchValue", typeof(Hashtable), container.Map, container.Map)!;
        var shouldSkipList = (bool)InvokeMerge("ShouldSkipPatchValue", typeof(List<object?>), container.Items, container.Items)!;
        var shouldSkipClass = (bool)InvokeMerge("ShouldSkipPatchValue", typeof(SampleChild), container.Child, container.Child)!;
        var shouldSkipNull = (bool)InvokeMerge("ShouldSkipPatchValue", typeof(string), null, "value")!;
        var shouldSkipScalar = (bool)InvokeMerge("ShouldSkipPatchValue", typeof(MergeMode), MergeMode.One, MergeMode.One)!;
        var shouldNotSkipScalar = (bool)InvokeMerge("ShouldSkipPatchValue", typeof(MergeMode), MergeMode.One, MergeMode.Two)!;

        Assert.Multiple(() =>
        {
            Assert.That(patchConfig["nested"], Is.Not.Null);
            Assert.That(patchDictionary["alpha"], Is.EqualTo("one"));
            Assert.That(patchList, Has.Count.EqualTo(2));
            Assert.That(patchClass["Name"], Is.EqualTo("node"));
            Assert.That(currentConfig["nested"], Is.Not.Null);
            Assert.That(currentDictionary["alpha"], Is.EqualTo("one"));
            Assert.That(currentList, Has.Count.EqualTo(2));
            Assert.That(currentClass["Name"], Is.EqualTo("node"));
            Assert.That(shouldSkipConfig, Is.True);
            Assert.That(shouldSkipDictionary, Is.True);
            Assert.That(shouldSkipList, Is.True);
            Assert.That(shouldSkipClass, Is.True);
            Assert.That(shouldSkipNull, Is.True);
            Assert.That(shouldSkipScalar, Is.True);
            Assert.That(shouldNotSkipScalar, Is.False);
        });
    }

    [Test]
    public void MergeUtility_PublicAndPrivateHelpers_CoverNullPatchesAndNestedEnumerableBranches()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Existing"] = "value"
            })
            .Build();
        var merged = configuration.MergeConfigurationObjectIntoIConfiguration(null);

        var convertedDictionary = (Dictionary<string, object?>)InvokeMerge("ConvertDictionary", new Hashtable
        {
            ["scalar"] = "value",
            ["nested-list"] = new ArrayList { "one", "two" },
            ["nested-class"] = new SampleChild { Name = "child" },
            ["null"] = null
        })!;
        var convertedList = (List<object?>)InvokeMerge("ConvertList", new ArrayList
        {
            "value",
            new ArrayList { "nested" },
            new SampleChild { Name = "child" },
            null
        })!;
        var currentDictionary = (Dictionary<string, object?>)InvokeMerge("ConvertCurrentDictionary", new Hashtable
        {
            ["value"] = null
        })!;
        var currentList = (List<object?>)InvokeMerge("ConvertCurrentList", new ArrayList { null, "text" })!;

        var anonymousProperty = new { Value = 1 }.GetType().GetProperty("Value")!;
        var anonymousPatchable = (bool)InvokeMerge("IsPatchableProperty", anonymousProperty)!;
        var equivalentNullMismatch = (bool)InvokeMerge("AreEquivalentValues", null, "value")!;
        var equivalentDictionaryMismatch = (bool)InvokeMerge("AreEquivalentValues",
            new Dictionary<string, object?> { ["left"] = 1 },
            new Dictionary<string, object?> { ["left"] = 2 })!;
        var equivalentListMismatch = (bool)InvokeMerge("AreEquivalentValues",
            new List<object?> { 1, 2 },
            new List<object?> { 1, 3 })!;

        Assert.Multiple(() =>
        {
            Assert.That(merged["Existing"], Is.EqualTo("value"));
            Assert.That(convertedDictionary, Does.ContainKey("scalar"));
            Assert.That(convertedDictionary["nested-list"], Is.InstanceOf<List<object?>>());
            Assert.That(convertedDictionary["nested-class"], Is.InstanceOf<Dictionary<string, object?>>());
            Assert.That(convertedDictionary.ContainsKey("null"), Is.False);
            Assert.That(convertedList, Has.Count.EqualTo(3));
            Assert.That(convertedList[1], Is.InstanceOf<List<object?>>());
            Assert.That(convertedList[2], Is.InstanceOf<Dictionary<string, object?>>());
            Assert.That(currentDictionary["value"], Is.Null);
            Assert.That(currentList, Is.EqualTo(new object?[] { null, "text" }));
            Assert.That(anonymousPatchable, Is.True);
            Assert.That(equivalentNullMismatch, Is.False);
            Assert.That(equivalentDictionaryMismatch, Is.False);
            Assert.That(equivalentListMismatch, Is.False);
        });
    }

    [Test]
    public void DictionaryUtility_FlatteningHelpers_HandleNestedConfigurationsCollectionsAndWhitespaceKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["child:value"] = "config"
            })
            .Build();
        var flattenedDictionary = (IDictionary<string, string?>)InvokeDictionaryUtils(
            "GetInMemoryCollectionFromDictionary",
            new Dictionary<string, object?>
            {
                ["scalar"] = "value",
                ["config"] = configuration,
                ["map"] = new Hashtable
                {
                    ["nested"] = "dictionary"
                },
                ["list"] = new ArrayList
                {
                    "first",
                    new Hashtable
                    {
                        ["inner"] = "second"
                    },
                    new SampleChild { Name = "child" }
                }
            },
            new Dictionary<string, string?>(),
            "root")!;
        var flattenedList = new Dictionary<string, string?>();
        InvokeDictionaryUtils("GetInMemoryCollectionFromList",
            new ArrayList
            {
                null,
                configuration,
                new Hashtable
                {
                    ["inner"] = "dictionary"
                },
                new ArrayList { "nested-list" },
                new SampleChild { Name = "from-object" }
            },
            "items",
            flattenedList);
        var stringDictionary = (Dictionary<string, object?>)InvokeDictionaryUtils("ToStringObjectDictionary",
            new Hashtable
            {
                ["answer"] = 42,
                [" "] = "ignored"
            })!;

        Assert.Multiple(() =>
        {
            Assert.That(flattenedDictionary["root:scalar"], Is.EqualTo("value"));
            Assert.That(flattenedDictionary["root:config:child:value"], Is.EqualTo("config"));
            Assert.That(flattenedDictionary["root:map:nested"], Is.EqualTo("dictionary"));
            Assert.That(flattenedDictionary["root:list:0"], Is.EqualTo("first"));
            Assert.That(flattenedDictionary["root:list:1:inner"], Is.EqualTo("second"));
            Assert.That(flattenedDictionary["root:list:2:Name"], Is.EqualTo("child"));
            Assert.That(flattenedList["items:0"], Is.EqualTo(string.Empty));
            Assert.That(flattenedList["items:1:child:value"], Is.EqualTo("config"));
            Assert.That(flattenedList["items:2:inner"], Is.EqualTo("dictionary"));
            Assert.That(flattenedList["items:3:0"], Is.EqualTo("nested-list"));
            Assert.That(flattenedList["items:4:Name"], Is.EqualTo("from-object"));
            Assert.That(stringDictionary.Keys, Is.EqualTo(new[] { "answer" }));
        });
    }

    [Test]
    public void DictionaryUtility_BindToDictionaryIConfiguration_CollapsesCaseInsensitiveDuplicatePaths()
    {
        var configuration = (IConfiguration)InvokeDictionaryUtils("BindToDictionaryIConfiguration",
            new Dictionary<string, object?>
            {
                ["Metadata"] = new Dictionary<string, object?>
                {
                    ["Team"] = "existing-team"
                },
                ["MetaData"] = new Dictionary<string, object?>
                {
                    ["Team"] = "updated-team"
                }
            })!;

        Assert.Multiple(() =>
        {
            Assert.That(configuration["MetaData:Team"], Is.EqualTo("updated-team"));
            Assert.That(configuration.AsEnumerable()
                .Count(pair => string.Equals(pair.Key, "MetaData:Team", StringComparison.OrdinalIgnoreCase)), Is.EqualTo(1));
        });
    }

    [Test]
    public void MergeUtility_PropertyHelpers_HandleAutoPropertiesAndThrowingGetters()
    {
        var autoProperty = typeof(GetterShapes).GetProperty(nameof(GetterShapes.Auto))!;
        var computedProperty = typeof(GetterShapes).GetProperty(nameof(GetterShapes.Computed))!;
        var brokenProperty = typeof(GetterShapes).GetProperty(nameof(GetterShapes.Broken))!;

        var brokenArgs = new object?[] { brokenProperty, new GetterShapes(), null };
        var okArgs = new object?[] { autoProperty, new GetterShapes(), null };

        var autoPatchable = (bool)InvokeMerge("IsPatchableProperty", autoProperty)!;
        var computedPatchable = (bool)InvokeMerge("IsPatchableProperty", computedProperty)!;
        var brokenSuccess = (bool)InvokeMerge("TryGetPropertyValue", brokenArgs)!;
        var autoSuccess = (bool)InvokeMerge("TryGetPropertyValue", okArgs)!;

        Assert.Multiple(() =>
        {
            Assert.That(autoPatchable, Is.True);
            Assert.That(computedPatchable, Is.False);
            Assert.That(brokenSuccess, Is.False);
            Assert.That(autoSuccess, Is.True);
            Assert.That(okArgs[2], Is.EqualTo("auto"));
        });
    }

    [Test]
    public void ConfigurationUtils_PrivateBindingHelpers_HandleListsDictionariesAndConfigurations()
    {
        var logger = new RecordingLogger();
        var binderOptions = new BinderOptions
        {
            ErrorOnUnknownConfiguration = true,
            BindNonPublicProperties = true
        };
        var configurationUtilsType = typeof(ConfigurationUtils);

        object? InvokeUtils(string methodName, params object?[] args) => configurationUtilsType
            .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, args);

        var boundDictionary = (IDictionary)InvokeUtils("BindDictionaryTypeProperty",
            new Dictionary<string, object?> { ["1"] = "one" },
            binderOptions,
            typeof(Dictionary<int, string>),
            logger,
            ":dict")!;
        var boundPair = (KeyValuePair<int, string>)InvokeUtils("BindDictionaryTypeProperty",
            new Dictionary<string, object?> { ["7"] = "seven" },
            binderOptions,
            typeof(KeyValuePair<int, string>),
            logger,
            ":pair")!;
        var invalidKeyException = Assert.Throws<TargetInvocationException>(() => InvokeUtils("BindDictionaryTypeProperty",
            new Dictionary<string, object?> { ["invalid"] = "value" },
            binderOptions,
            typeof(Dictionary<MergeMode, string>),
            logger,
            ":enumDict"));
        var listFromDictionary = (IList)InvokeUtils("ConvertObjectValueToType",
            new Dictionary<string, object?> { ["0"] = "4", ["1"] = "5" },
            binderOptions,
            typeof(int[]),
            logger,
            ":values")!;
        var dictionaryFromList = (IDictionary)InvokeUtils("ConvertObjectValueToType",
            new List<object?> { "a", "b" },
            binderOptions,
            typeof(Dictionary<int, string>),
            logger,
            ":map")!;
        var configurationFromScalar = (IConfiguration)InvokeUtils("ConvertObjectValueToType",
            7,
            binderOptions,
            typeof(IConfiguration),
            logger,
            ":scalar")!;
        var configurationFromList = (IConfiguration)InvokeUtils("ConvertObjectValueToType",
            new List<object?> { "x" },
            binderOptions,
            typeof(IConfiguration),
            logger,
            ":listConfig")!;
        var boundUnsupportedInterfaceException = Assert.Throws<TargetInvocationException>(() => InvokeUtils("BindToObject",
            typeof(IDisposable),
            new Dictionary<string, object?>(),
            binderOptions,
            logger,
            string.Empty))!;
        var nullString = InvokeUtils("ConvertObjectValueToType", null, binderOptions, typeof(string), logger, ":text");
        var invalidEnumList = (IList)InvokeUtils("CreateListFromTypeAndConvertConfigurationListToIt",
            typeof(List<MergeMode>),
            false,
            new List<object?> { "Missing" },
            binderOptions,
            logger,
            ":enumList")!;
        var trimmedList = (IList)InvokeUtils("CreateListFromTypeAndConvertConfigurationListToIt",
            typeof(List<int>),
            false,
            new List<object?> { 1, null, 2 },
            binderOptions,
            logger,
            ":trimmed")!;
        var indexedDictionary = (IDictionary)InvokeUtils("ConvertConfigurationListToDictionaryWithIndexesAsKeys",
            new List<object?> { "left", "right" },
            typeof(Dictionary<int, string>),
            binderOptions,
            logger,
            ":indexed")!;

        Assert.Multiple(() =>
        {
            Assert.That(boundDictionary[1], Is.EqualTo("one"));
            Assert.That(boundPair, Is.EqualTo(new KeyValuePair<int, string>(7, "seven")));
            Assert.That(invalidKeyException!.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(listFromDictionary.Cast<object>(), Is.EqualTo(new object[] { 4, 5 }));
            Assert.That(dictionaryFromList[0], Is.EqualTo("a"));
            Assert.That(dictionaryFromList[1], Is.EqualTo("b"));
            Assert.That(configurationFromScalar[string.Empty], Is.EqualTo("7"));
            Assert.That(configurationFromList.AsEnumerable().Any(pair => pair.Value == "x"), Is.True);
            Assert.That(boundUnsupportedInterfaceException.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(nullString, Is.EqualTo(string.Empty));
            Assert.That(invalidEnumList, Is.Empty);
            Assert.That(trimmedList.Cast<object>(), Is.EqualTo(new object[] { 1, 2 }));
            Assert.That(indexedDictionary[0], Is.EqualTo("left"));
            Assert.That(indexedDictionary[1], Is.EqualTo("right"));
            Assert.Throws<TargetInvocationException>(() => InvokeUtils("BindDictionaryTypeProperty",
                new Dictionary<string, object?>(),
                binderOptions,
                typeof(List<int>),
                logger,
                ":bad"));
        });
    }

    [Test]
    public void ConfigurationUtils_PublicBranches_HandleNullObjectsEmptyKeysAndInterfaceBindingFailures()
    {
        var logger = new RecordingLogger();
        var binderOptions = new BinderOptions
        {
            ErrorOnUnknownConfiguration = true,
            BindNonPublicProperties = true
        };
        var configurationUtilsType = typeof(ConfigurationUtils);

        object? InvokeUtils(string methodName, params object?[] args) => configurationUtilsType
            .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, args);

        var emptyCollection = ConfigurationUtils.GetInMemoryCollectionFromObject(null);
        var nullScalarCollection = ConfigurationUtils.GetInMemoryCollectionFromObject(new NullableScalarHolder());
        var reboundDictionary = (IDictionary)InvokeUtils("BindToObject",
            typeof(Dictionary<int, string>),
            new Dictionary<string, object?> { [string.Empty] = new List<object?> { "left", "right" } },
            binderOptions,
            logger,
            ":root")!;
        var nullInt = InvokeUtils("ConvertObjectValueToType", null, binderOptions, typeof(int), logger, ":number");
        var publicBindException = Assert.Throws<ArgumentException>(() =>
            new ConfigurationBuilder().Build().BindToObject(typeof(IDisposable), binderOptions, logger));

        Assert.Multiple(() =>
        {
            Assert.That(emptyCollection, Is.Empty);
            Assert.That(nullScalarCollection.ContainsKey(nameof(NullableScalarHolder.Maybe)), Is.True);
            Assert.That(nullScalarCollection[nameof(NullableScalarHolder.Maybe)], Is.Null);
            Assert.That(reboundDictionary[0], Is.EqualTo("left"));
            Assert.That(reboundDictionary[1], Is.EqualTo("right"));
            Assert.That(nullInt, Is.EqualTo(0));
            Assert.That(publicBindException, Is.TypeOf<ArgumentException>());
        });
    }

    [Test]
    public void MergeUtility_AdditionalBranches_HandleDefaultlessTypesAndEquivalenceEdgeCases()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["nested:value"] = "1"
            })
            .Build();

        var patchDictionary = (Dictionary<string, object?>)InvokeMerge("GetPatchDictionary", new NoDefaultCtorSample("v"))!;
        var nullPatch = InvokeMerge("ConvertPatchValue", typeof(SampleChild), null);
        var convertedDictionary = (Dictionary<string, object?>)InvokeMerge("ConvertDictionary", new Hashtable
        {
            ["config"] = config,
            ["nested-dictionary"] = new Hashtable
            {
                ["inner"] = "value"
            }
        })!;
        var convertedList = (List<object?>)InvokeMerge("ConvertList", new ArrayList
        {
            config,
            new Hashtable
            {
                ["inner"] = "value"
            }
        })!;
        var skipConfigAgainstDictionary = (bool)InvokeMerge("ShouldSkipPatchValue",
            typeof(IConfiguration),
            config,
            new Dictionary<string, object?> { ["nested"] = "other" })!;
        var getOnlyAutoPatchable = (bool)InvokeMerge("IsPatchableProperty",
            typeof(GetOnlyAutoHolder).GetProperty(nameof(GetOnlyAutoHolder.Value))!)!;
        var unequalDictionaryKeys = (bool)InvokeMerge("AreEquivalentValues",
            new Dictionary<string, object?> { ["left"] = 1 },
            new Dictionary<string, object?> { ["right"] = 1 })!;
        var unequalListLengths = (bool)InvokeMerge("AreEquivalentValues",
            new List<object?> { 1 },
            new List<object?> { 1, 2 })!;

        Assert.Multiple(() =>
        {
            Assert.That(patchDictionary[nameof(NoDefaultCtorSample.Value)], Is.EqualTo("v"));
            Assert.That(nullPatch, Is.Null);
            Assert.That(convertedDictionary["config"], Is.InstanceOf<Dictionary<string, object?>>());
            Assert.That(convertedDictionary["nested-dictionary"], Is.InstanceOf<Dictionary<string, object?>>());
            Assert.That(convertedList[0], Is.InstanceOf<Dictionary<string, object?>>());
            Assert.That(convertedList[1], Is.InstanceOf<Dictionary<string, object?>>());
            Assert.That(skipConfigAgainstDictionary, Is.False);
            Assert.That(getOnlyAutoPatchable, Is.True);
            Assert.That(unequalDictionaryKeys, Is.False);
            Assert.That(unequalListLengths, Is.False);
        });
    }
}
