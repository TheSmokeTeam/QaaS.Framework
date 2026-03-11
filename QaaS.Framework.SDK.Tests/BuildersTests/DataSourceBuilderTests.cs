using System.Collections.Immutable;
using System.Reflection;
using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Generator;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.SDK.Tests.BuildersTests;

[TestFixture]
public class DataSourceBuilderTests
{
    private static PropertyInfo _generatorInfo =
        typeof(DataSourceBuilder).GetProperty("Generator", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static IEnumerable<TestCaseData> GetDataSourcesBuilders()
    {
        var jsonSerializerConfig = new SerializeConfig { Serializer = SerializationType.Json };
        // Test case 1: DataSourceBuilder that references another datasource out of 3 other builders' datasources
        var builder1 = new DataSourceBuilder()
            .Named("dependent-source-1")
            .HookNamed("TestGenerator")
            .IsLazy()
            .WithSerializer(jsonSerializerConfig)
            .AddDataSourceName("dependent-source-2");

        var builder2 = new DataSourceBuilder()
            .Named("dependent-source-2")
            .HookNamed("TestGenerator")
            .AddDataSourceName("dependent-source-3");

        var builder3 = new DataSourceBuilder()
            .Named("dependent-source-3")
            .HookNamed("TestGenerator")
            .WithSerializer(jsonSerializerConfig)
            .AddDataSourceName("dependent-source-1");

        yield return new TestCaseData(
            new List<DataSourceBuilder> { builder1, builder2, builder3 },
            new List<string> { "dependent-source-2" }
        ).SetName("DataSourceBuilder referencing one of three datasources");

        // Test case 2: DataSourceBuilder that references nothing (should reference all)
        var builder4 = new DataSourceBuilder()
            .Named("reference-all-source")
            .HookNamed("TestGenerator");

        var builder5 = new DataSourceBuilder()
            .Named("source-5")
            .IsLazy()
            .WithSerializer(jsonSerializerConfig)
            .HookNamed("TestGenerator");

        var builder6 = new DataSourceBuilder()
            .Named("source-6")
            .HookNamed("TestGenerator");

        yield return new TestCaseData(
            new List<DataSourceBuilder> { builder4, builder5, builder6 },
            new List<string> { "source-5", "source-6" }
        ).SetName("DataSourceBuilder referencing all datasources");

        // Test case 3: DataSourceBuilder that tries to reference a non-existing datasource
        var builder7 = new DataSourceBuilder()
            .Named("non-existing-reference")
            .HookNamed("TestGenerator")
            .AddDataSourceName("non-existing-source");

        var builder8 = new DataSourceBuilder()
            .Named("existing-source-1")
            .HookNamed("TestGenerator");

        var builder9 = new DataSourceBuilder()
            .IsLazy()
            .WithSerializer(jsonSerializerConfig)
            .Named("existing-source-2")
            .HookNamed("TestGenerator");

        yield return new TestCaseData(
            new List<DataSourceBuilder> { builder7, builder8, builder9 },
            null
        ).SetName("DataSourceBuilder referencing non-existing datasource");
    }

    [Test]
    [TestCaseSource(nameof(GetDataSourcesBuilders))]
    public void
        TestBuild_CallOnDataSourcesWithReferencesToOthers_ExpectAllDataSourcesToBuildAndTheReferencedToBeInDataSourceList(
            List<DataSourceBuilder> builders, List<string>? expectedReferences)
    {
        // arrange
        // Create test generators
        var generators = builders
            .Select(dsb => new KeyValuePair<string, IGenerator>(dsb.Name!, new TestGenerator()))
            .ToDictionary();

        // Register all builders
        var registeredDataSources = builders.Select(b => b.Register()).ToList();

        // act
        DataSource? firstDataSource = null;
        try
        {
            firstDataSource = builders.Select(builder => builder.Build(Globals.GetContextWithMetadata(), registeredDataSources, generators))
                .ToList().FirstOrDefault();
        }
        catch (ArgumentException e) when (e.Message.Contains("Item") && e.Message.Contains("not found in"))
        {
            Globals.Logger.LogDebug("Configured item not found in generators as expected - {ExceptionMessage}",
                e.Message);
        }

        // assert
        if (firstDataSource != null)
        {
            var resultedNames = firstDataSource.DataSourceList.Select(ds => ds.Name).ToList();
            Assert.That(resultedNames.All(expectedReferences!.Contains));
            Assert.That(resultedNames.All((builders.FirstOrDefault()?.DataSourceNames.ToList() ?? []).Contains));
            Assert.That(firstDataSource.DataSourceList.All(registeredDataSources.Contains));
            Assert.That(firstDataSource.DataSourceList.Select(source => source.Generator)
                .All(generators.Values.Contains));
        }
        else
        {
            Assert.That(firstDataSource, Is.Null);
            Assert.That(expectedReferences, Is.Null);
        }
    }

    [Test]
    public void TestDataSourceBuilderCreation()
    {
        var builder = new DataSourceBuilder()
            .Named("basic-source")
            .HookNamed("simple-generator")
            .IsLazy();

        Assert.That(builder, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(builder.Name, Is.Not.Null.Or.Empty);
            Assert.That(_generatorInfo.GetValue(builder), Is.Not.Null.Or.Empty);
        });
    }

    [Test]
    public void TestDataSourceBuilderRegistration()
    {
        var builder = new DataSourceBuilder()
            .Named("registration-test")
            .HookNamed("test-generator")
            .IsLazy();

        var dataSource = builder.Register();

        Assert.That(dataSource, Is.Not.Null);
        Assert.That(dataSource.Name, Is.EqualTo("registration-test"));
        Assert.That(dataSource.Lazy, Is.True);
        Assert.That(dataSource.Generator, Is.Null); // Generator not set until Build() is called
    }

    [Test]
    public void TestDataSourceBuilderBuild()
    {
        // Create test generators
        var generators = new Dictionary<string, IGenerator>
        {
            ["test-source"] = new TestGenerator()
        };

        // Create test data sources
        var dataSources = new List<DataSource>
        {
            new() { Name = "source-a", Serializer = SerializerFactory.BuildSerializer(SerializationType.Binary) },
            new() { Name = "source-b", Deserializer = DeserializerFactory.BuildDeserializer(SerializationType.Json) }
        };

        var builder = new DataSourceBuilder()
            .Named("test-source")
            .HookNamed("test-generator")
            .AddDataSourceName("source-a")
            .AddDataSourceName("source-b");

        var dataSource = builder.Register();
        var builtDataSource = builder.Build(Globals.GetContextWithMetadata(), dataSources, generators);

        Assert.That(builtDataSource, Is.Not.Null);
        Assert.That(builtDataSource.Name, Is.EqualTo("test-source"));
        Assert.That(builtDataSource.Generator, Is.Not.Null);
        Assert.That(builtDataSource.DataSourceList, Has.Count.EqualTo(2));
        Assert.That(builtDataSource.DataSourceList.All(dataSourceFiltered => dataSources.Contains(dataSourceFiltered)));
    }

    [Test]
    public void ConfigurationCrud_ReadUpdateAndDelete_WorkAsExpected()
    {
        var builder = new DataSourceBuilder()
            .Configure(new
            {
                Existing = "value",
                Nested = new
                {
                    Before = "keep"
                }
            });

        var initialConfiguration = builder.ReadConfiguration();
        builder.UpdateConfiguration(new
        {
            Nested = new
            {
                Added = "new"
            }
        });
        var updatedConfiguration = builder.ReadConfiguration();
        builder.DeleteConfiguration();

        Assert.Multiple(() =>
        {
            Assert.That(initialConfiguration["Existing"], Is.EqualTo("value"));
            Assert.That(initialConfiguration["Nested:Before"], Is.EqualTo("keep"));
            Assert.That(updatedConfiguration["Existing"], Is.EqualTo("value"));
            Assert.That(updatedConfiguration["Nested:Before"], Is.EqualTo("keep"));
            Assert.That(updatedConfiguration["Nested:Added"], Is.EqualTo("new"));
            Assert.That(builder.ReadConfiguration().GetChildren(), Is.Empty);
        });
    }
}

public class TestGenerator : BaseGenerator<object>
{
    public override IEnumerable<Data<object>> Generate(IImmutableList<SessionData> sessionDataList,
        IImmutableList<DataSource> dataSourceList)
    {
        return [];
    }
}
