using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using QaaS.Framework.Configurations.ConfigurationBindingUtils;
using QaaS.Framework.Configurations.ConfigurationBuilderExtensions;
using QaaS.Framework.Configurations.CustomExceptions;
using QaaS.Framework.Configurations.References;

namespace QaaS.Framework.Configurations.Tests;

[TestFixture]
public class ConfigurationUtilitiesTests
{
    private enum ExampleMode
    {
        First,
        Second
    }

    private sealed class NestedSettings
    {
        public string? Name { get; set; }
    }

    private sealed class ComplexSettings
    {
        public int Number { get; set; }
        public ExampleMode? Mode { get; set; }
        public NestedSettings Child { get; set; } = new();
        public List<int> Values { get; set; } = [];
        public Dictionary<string, object?> Map { get; set; } = [];
        public IConfiguration? Section { get; set; }
    }

    private sealed class MergePatchSettings
    {
        public string Url { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public int Retries { get; set; } = 3;
        public NestedSettings Child { get; set; } = new();
    }

    private sealed class RequiredSettings
    {
        [Required]
        public string? Name { get; set; }
    }

    private sealed class InvalidChild
    {
        [Required]
        public string? RequiredValue { get; set; }
    }

    private sealed class RecursiveValidationRoot
    {
        public List<InvalidChild> Items { get; set; } = [];
        public Dictionary<string, InvalidChild> ByName { get; set; } = [];
    }

    [Test]
    public void BuildConfigurationAsYaml_UsesRequestedSectionOrder()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["zeta:value"] = "1",
                ["alpha:value"] = "2"
            })
            .Build();

        var yaml = configuration.BuildConfigurationAsYaml(["alpha", "zeta"]);

        var alphaIndex = yaml.IndexOf("alpha:", StringComparison.Ordinal);
        var zetaIndex = yaml.IndexOf("zeta:", StringComparison.Ordinal);
        Assert.That(alphaIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(zetaIndex, Is.GreaterThan(alphaIndex));
    }

    [Test]
    public void GetInMemoryCollectionFromObject_FlattensComplexObjectGraph()
    {
        var configurationObject = new ComplexSettings
        {
            Number = 9,
            Mode = ExampleMode.Second,
            Child = new NestedSettings { Name = "nested" },
            Values = [4, 5],
            Map = new Dictionary<string, object?> { ["k"] = "v" },
            Section = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["inner:value"] = "x" })
                .Build()
        };

        var flat = ConfigurationUtils.GetInMemoryCollectionFromObject(configurationObject);

        Assert.Multiple(() =>
        {
            Assert.That(flat["Number"], Is.EqualTo("9"));
            Assert.That(flat["Mode"], Is.EqualTo(ExampleMode.Second.ToString()));
            Assert.That(flat["Child:Name"], Is.EqualTo("nested"));
            Assert.That(flat["Values:0"], Is.EqualTo("4"));
            Assert.That(flat["Map:k"], Is.EqualTo("v"));
            Assert.That(flat["Section:inner:value"], Is.EqualTo("x"));
        });
    }

    [Test]
    public void GetInMemoryCollectionFromObject_WithNonClass_Throws()
    {
        Assert.Throws<ArgumentException>(() => ConfigurationUtils.GetInMemoryCollectionFromObject(5));
    }

    [Test]
    public void BindToObject_BindsNestedCollectionsDictionariesAndEnums()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Number"] = "7",
                ["Mode"] = "Second",
                ["Child:Name"] = "child",
                ["Values:0"] = "1",
                ["Values:1"] = "2",
                ["Map:a"] = "A",
                ["Section:inner"] = "v"
            })
            .Build();

        var bound = configuration.BindToObject<ComplexSettings>(new BinderOptions
        {
            ErrorOnUnknownConfiguration = true,
            BindNonPublicProperties = false
        });

        Assert.Multiple(() =>
        {
            Assert.That(bound.Number, Is.EqualTo(7));
            Assert.That(bound.Mode, Is.EqualTo(ExampleMode.Second));
            Assert.That(bound.Child.Name, Is.EqualTo("child"));
            Assert.That(bound.Values, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(bound.Map["a"], Is.EqualTo("A"));
            Assert.That(bound.Section?["inner"], Is.EqualTo("v"));
        });
    }

    [Test]
    public void LoadAndValidateConfiguration_ThrowsForInvalidObject()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();

        Assert.Throws<InvalidConfigurationsException>(
            () => configuration.LoadAndValidateConfiguration<RequiredSettings>());
    }

    [Test]
    public void LoadAndValidateConfiguration_ReturnsValidObject()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Name"] = "ok" })
            .Build();

        var loaded = configuration.LoadAndValidateConfiguration<RequiredSettings>();

        Assert.That(loaded.Name, Is.EqualTo("ok"));
    }

    [Test]
    public void PlaceholderParser_ResolvesStringDefaultsAndObjectCopy()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["source:value"] = "live",
                ["target"] = "${source:value}",
                ["fallback"] = "${missing??default-value}",
                ["obj:child:id"] = "42",
                ["objCopy"] = "${obj}"
            })
            .Build();

        var parsed = new ConfigurationPlaceholderParser(configuration).ResolvePlaceholders();

        Assert.Multiple(() =>
        {
            Assert.That(parsed["target"], Is.EqualTo("live"));
            Assert.That(parsed["fallback"], Is.EqualTo("default-value"));
            Assert.That(parsed["objCopy:child:id"], Is.EqualTo("42"));
        });
    }

    [Test]
    public void PlaceholderParser_CircularReference_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["a"] = "${b}",
                ["b"] = "${a}"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(
            () => new ConfigurationPlaceholderParser(configuration).ResolvePlaceholders());
    }

    [Test]
    public void PlaceholderParser_ObjectPlaceholderUsedAsSubstring_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["obj:child:id"] = "42",
                ["target"] = "prefix-${obj}-suffix"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(
            () => new ConfigurationPlaceholderParser(configuration).ResolvePlaceholders());
    }

    [Test]
    public void PlaceholderParser_MissingPlaceholderWithoutDefault_RemainsUnchanged()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["target"] = "${missing:path}"
            })
            .Build();

        var parsed = new ConfigurationPlaceholderParser(configuration).ResolvePlaceholders();

        Assert.That(parsed["target"], Is.EqualTo("${missing:path}"));
    }

    [Test]
    public void CollapseShiftLeftArrowsInConfiguration_CollapsesChildren()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["root:<<:shared:value"] = "1",
                ["root:local:value"] = "2"
            })
            .Build();

        var collapsed = configuration.CollapseShiftLeftArrowsInConfiguration();

        Assert.Multiple(() =>
        {
            Assert.That(collapsed["root:shared:value"], Is.EqualTo("1"));
            Assert.That(collapsed["root:local:value"], Is.EqualTo("2"));
        });
    }

    [Test]
    public void CollapseShiftLeftArrowsInConfiguration_WithValueUnderCollapseKey_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["root:<<"] = "invalid"
            })
            .Build();

        Assert.Throws<InvalidConfigurationsException>(() => configuration.CollapseShiftLeftArrowsInConfiguration());
    }

    [Test]
    public void ResolveReferencesInConfiguration_ReplacesKeywordAndShiftsIndexes()
    {
        var referenceFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.yaml");
        File.WriteAllText(referenceFile,
            "items:\n  - id: ref-a\n    v: 1\n  - id: ref-b\n    v: 2\n");

        try
        {
            var baseConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["items:0:id"] = "local",
                    ["items:1:id"] = "__REF__",
                    ["items:2:id"] = "tail"
                })
                .Build();

            var resolved = baseConfiguration.ResolveReferencesInConfiguration(
                new[]
                {
                    new ReferenceConfig
                    {
                        ReferenceReplaceKeyword = "__REF__",
                        ReferenceFilesPaths = [referenceFile]
                    }
                },
                ["items"],
                [@"items:\d+:id"],
                resolveReferencesWithEnvironmentVariables: false);

            Assert.Multiple(() =>
            {
                Assert.That(resolved["items:0:id"], Is.EqualTo("local"));
                Assert.That(resolved["items:1:id"], Is.EqualTo("__REF__ref-a"));
                Assert.That(resolved["items:2:id"], Is.EqualTo("__REF__ref-b"));
                Assert.That(resolved["items:3:id"], Is.EqualTo("tail"));
            });
        }
        finally
        {
            File.Delete(referenceFile);
        }
    }

    [Test]
    public void ResolveReferencesInConfiguration_WithMultipleReplaceKeywords_Throws()
    {
        var referenceFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.yaml");
        File.WriteAllText(referenceFile, "items:\n  - id: ref-a\n");

        try
        {
            var baseConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["items:0:id"] = "__REF__",
                    ["items:1:id"] = "__REF__"
                })
                .Build();

            Assert.Throws<InvalidConfigurationsException>(() =>
                baseConfiguration.ResolveReferencesInConfiguration(
                    new[]
                    {
                        new ReferenceConfig
                        {
                            ReferenceReplaceKeyword = "__REF__",
                            ReferenceFilesPaths = [referenceFile]
                        }
                    },
                    ["items"],
                    null,
                    resolveReferencesWithEnvironmentVariables: false));
        }
        finally
        {
            File.Delete(referenceFile);
        }
    }

    [Test]
    public void AddYaml_LoadsYamlFromAbsolutePath()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.yaml");
        File.WriteAllText(filePath, "root:\n  child: from-file\n");

        try
        {
            var configuration = new ConfigurationBuilder().AddYaml(filePath).Build();
            Assert.That(configuration["root:child"], Is.EqualTo("from-file"));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Test]
    public void AddYamlFromHttpGet_WithInvalidUrl_ThrowsCouldNotFindConfigurationException()
    {
        var configurationBuilder = new ConfigurationBuilder()
            .AddYamlFromHttpGet("http://127.0.0.1:1/non-existing.yaml", TimeSpan.FromMilliseconds(100));

        Assert.Throws<CouldNotFindConfigurationException>(() => configurationBuilder.Build());
    }

    [Test]
    public void PathUtils_IsPathHttpUrl_ReturnsExpectedValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PathUtils.IsPathHttpUrl("http://x"), Is.True);
            Assert.That(PathUtils.IsPathHttpUrl("https://x"), Is.True);
            Assert.That(PathUtils.IsPathHttpUrl("c:\\tmp\\a.yaml"), Is.False);
        });
    }

    [Test]
    public void ValidationUtils_TryValidateObjectRecursive_ValidatesCollectionsAndAddsPathPrefixes()
    {
        var root = new RecursiveValidationRoot
        {
            Items = [new InvalidChild()],
            ByName = new Dictionary<string, InvalidChild>
            {
                ["node-a"] = new()
            }
        };
        var results = new List<ValidationResult>();

        var valid = ValidationUtils.TryValidateObjectRecursive(root, results, "root");

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(results, Is.Not.Empty);
            Assert.That(results.Any(r => r.ErrorMessage?.Contains("Items:0") == true), Is.True);
            Assert.That(results.Any(r => r.ErrorMessage?.Contains("ByName:node-a") == true), Is.True);
        });
    }

    [Test]
    public void IConfigurationUtils_BindConfigurationObjectToIConfiguration_MergesOnlyNonDefaultPatchFields()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Url"] = "https://existing",
                ["Enabled"] = "True",
                ["Retries"] = "7",
                ["Child:Name"] = "original-child"
            })
            .Build();

        var rebound = configuration.BindConfigurationObjectToIConfiguration(new MergePatchSettings
        {
            Enabled = false,
            Child = new NestedSettings { Name = "updated-child" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(rebound["Url"], Is.EqualTo("https://existing"));
            Assert.That(rebound["Enabled"], Is.EqualTo("False"));
            Assert.That(rebound["Retries"], Is.EqualTo("7"));
            Assert.That(rebound["Child:Name"], Is.EqualTo("updated-child"));
        });
    }

    [Test]
    public void ConfigurationMerge_MergesAgainstFreshDefaultInstances()
    {
        var currentConfiguration = new MergePatchSettings
        {
            Url = "https://existing",
            Enabled = true,
            Retries = 8,
            Child = new NestedSettings { Name = "original-child" }
        };

        var mergedConfiguration = currentConfiguration.MergeConfiguration(new MergePatchSettings
        {
            Enabled = false,
            Retries = 0,
            Child = new NestedSettings { Name = "updated-child" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(mergedConfiguration, Is.Not.Null);
            Assert.That(mergedConfiguration!.Url, Is.EqualTo("https://existing"));
            Assert.That(mergedConfiguration.Enabled, Is.False);
            Assert.That(mergedConfiguration.Retries, Is.Zero);
            Assert.That(mergedConfiguration.Child.Name, Is.EqualTo("updated-child"));
        });
    }
}
