using Microsoft.Extensions.Configuration;

namespace QaaS.Framework.Configurations.Tests;

[TestFixture]
public class ConfigurationUpdateExtensionsTests
{
    [Test]
    public void UpdateConfiguration_WithNullCurrentConfiguration_ReturnsIncomingConfiguration()
    {
        FirstConfig? current = null;

        var updated = current.UpdateConfiguration(new FirstConfig
        {
            Name = "incoming"
        });

        Assert.That(updated.Name, Is.EqualTo("incoming"));
    }

    [Test]
    public void UpdateConfiguration_WithNullIncomingConfiguration_ThrowsArgumentNullException()
    {
        var current = new FirstConfig();

        Assert.Throws<ArgumentNullException>(() => current.UpdateConfiguration<FirstConfig>(null!));
    }

    [Test]
    public void UpdateConfiguration_WithDifferentRuntimeTypes_ReplacesCurrentConfiguration()
    {
        ITestConfig current = new FirstConfig
        {
            Name = "before"
        };

        var updated = current.UpdateConfiguration<ITestConfig>(new SecondConfig
        {
            Count = 3
        });

        Assert.That(updated, Is.TypeOf<SecondConfig>());
        Assert.That(((SecondConfig)updated).Count, Is.EqualTo(3));
    }

    [Test]
    public void UpdateConfiguration_WithSparseSameTypeUpdate_PreservesExistingStringsAndNestedValues()
    {
        ITestConfig current = new FirstConfig
        {
            Name = "existing",
            TimeoutSeconds = 30,
            Nested = new NestedConfig
            {
                Marker = "nested-existing"
            },
            Tags = ["one"]
        };

        var updated = current.UpdateConfiguration<ITestConfig>(new FirstConfig
        {
            TimeoutSeconds = 5,
            Nested = new NestedConfig
            {
                Marker = string.Empty
            }
        });

        var typedUpdated = (FirstConfig)updated;
        Assert.Multiple(() =>
        {
            Assert.That(typedUpdated.Name, Is.EqualTo("existing"));
            Assert.That(typedUpdated.TimeoutSeconds, Is.EqualTo(5));
            Assert.That(typedUpdated.Nested.Marker, Is.EqualTo("nested-existing"));
            Assert.That(typedUpdated.Tags, Is.EqualTo(new[] { "one" }));
        });
    }

    [Test]
    public void UpdateConfiguration_WithObjectPatch_PreservesExistingStringsAndMergesNestedValues()
    {
        ITestConfig current = new FirstConfig
        {
            Name = "existing",
            TimeoutSeconds = 30,
            Nested = new NestedConfig
            {
                Marker = "nested-existing"
            },
            Tags = ["one"]
        };

        var updated = current.UpdateConfiguration<ITestConfig>(new
        {
            TimeoutSeconds = 12,
            Nested = new
            {
                Marker = "nested-updated"
            }
        });

        var typedUpdated = (FirstConfig)updated;
        Assert.Multiple(() =>
        {
            Assert.That(typedUpdated.Name, Is.EqualTo("existing"));
            Assert.That(typedUpdated.TimeoutSeconds, Is.EqualTo(12));
            Assert.That(typedUpdated.Nested.Marker, Is.EqualTo("nested-updated"));
            Assert.That(typedUpdated.Tags, Is.EqualTo(new[] { "one" }));
        });
    }

    [Test]
    public void UpdateConfiguration_WithTypeWithoutDefaultConstructor_StillAppliesIncomingValues()
    {
        var current = new NoDefaultConfig("before")
        {
            Count = 1
        };

        var updated = current.UpdateConfiguration(new NoDefaultConfig("after")
        {
            Count = 2
        });

        Assert.Multiple(() =>
        {
            Assert.That(updated.Label, Is.EqualTo("after"));
            Assert.That(updated.Count, Is.EqualTo(2));
        });
    }

    [Test]
    public void UpdateConfiguration_ForRawConfiguration_BindsOntoExistingTree()
    {
        var current = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Feature:Enabled"] = "true"
            })
            .Build();

        var updated = current.UpdateConfiguration(new
        {
            Feature = new
            {
                Threshold = 5
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(updated["Feature:Enabled"], Is.EqualTo("true"));
            Assert.That(updated["Feature:Threshold"], Is.EqualTo("5"));
        });
    }

    [Test]
    public void UpdateConfiguration_ForRawConfiguration_WithIndexedListPatch_ReplacesExistingListIndexes()
    {
        var current = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InputNames"] = "scalar-that-should-not-survive",
                ["InputNames:0"] = "Name1",
                ["InputNames:1"] = "StaleName"
            })
            .Build();

        var updated = current.UpdateConfiguration(new
        {
            InputNames = new[] { "Name2" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(updated["InputNames:0"], Is.EqualTo("Name2"));
            Assert.That(updated["InputNames:1"], Is.Null);
            Assert.That(updated["InputNames"], Is.Null);
            Assert.That(updated.AsEnumerable().Count(pair =>
                pair.Key.StartsWith("InputNames:", StringComparison.OrdinalIgnoreCase) &&
                pair.Value != null), Is.EqualTo(1));
        });
    }

    [Test]
    public void UpdateConfiguration_WithObjectPatchAndIndexedList_ReplacesExistingListValues()
    {
        var current = new IndexedConfig
        {
            InputNames = ["Name1", "StaleName"]
        };

        var updated = current.UpdateConfiguration(new
        {
            InputNames = new[] { "Name2" }
        });

        Assert.That(updated.InputNames, Is.EqualTo(new[] { "Name2" }));
    }

    [Test]
    public void UpdateConfiguration_ForRawConfiguration_WithNullCurrentConfiguration_CreatesNewTree()
    {
        IConfiguration? current = null;

        var updated = current.UpdateConfiguration(new
        {
            Feature = new
            {
                Enabled = true
            }
        });

        Assert.That(updated["Feature:Enabled"], Is.EqualTo("True"));
    }

    [Test]
    public void UpdateConfiguration_WithObjectPatchAndNullInterfaceCurrent_ThrowsInvalidOperationException()
    {
        ITestConfig? current = null;

        var exception = Assert.Throws<InvalidOperationException>(() => current.UpdateConfiguration<ITestConfig>(new
        {
            Name = "incoming"
        }));

        Assert.That(exception!.Message, Does.Contain(nameof(ITestConfig)));
    }

    private interface ITestConfig
    {
    }

    private sealed class FirstConfig : ITestConfig
    {
        public string Name { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; }
        public NestedConfig Nested { get; set; } = new();
        public string[] Tags { get; set; } = [];
    }

    private sealed class SecondConfig : ITestConfig
    {
        public int Count { get; set; }
    }

    private sealed class NestedConfig
    {
        public string Marker { get; set; } = string.Empty;
    }

    private sealed class NoDefaultConfig(string label)
    {
        public string Label { get; set; } = label;
        public int Count { get; set; }
    }

    private sealed class IndexedConfig
    {
        public string[] InputNames { get; set; } = [];
    }
}
