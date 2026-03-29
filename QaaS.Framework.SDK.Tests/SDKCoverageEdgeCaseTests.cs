using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using QaaS.Framework.SDK.ConfigurationObjectFilters;
using QaaS.Framework.SDK.ConfigurationObjects;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Hooks.Assertion;
using QaaS.Framework.SDK.Hooks.BaseHooks;
using QaaS.Framework.SDK.Hooks.Generator;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects.RunningSessionsObjects;
using QaaS.Framework.Serialization;
using YamlDotNet.Serialization;

namespace QaaS.Framework.SDK.Tests;

[TestFixture]
public class SDKCoverageEdgeCaseTests
{
    private sealed class RequiredContextConfig
    {
        [Required]
        public string? Name { get; set; }
    }

    private sealed class BuilderGenerator : BaseGenerator<object>
    {
        public override IEnumerable<Data<object>> Generate(IImmutableList<SessionData> sessionDataList,
            IImmutableList<DataSource> dataSourceList) => [];
    }

    [Test]
    public void EnumerableExtensions_AsSingle_ValidatesCardinality()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new[] { 5 }.AsSingle(), Is.EqualTo(5));
            Assert.Throws<ArgumentNullException>(() => EnumerableExtensions.AsSingle<int>(null!));
            Assert.Throws<ArgumentException>(() => Array.Empty<int>().AsSingle());
            Assert.Throws<ArgumentException>(() => new[] { 1, 2 }.AsSingle());
        });
    }

    [Test]
    public void EnumerableExtensions_GetFilteredConfigurationObjectList_HandlesNullAndMissingEntries()
    {
        var dataSources = ImmutableList.Create(
            new DataSource { Name = "dep-a" },
            new DataSource { Name = "dep-b" });

        var empty = EnumerableExtensions.GetFilteredConfigurationObjectList<DataSource, string>(
            dataSources,
            null,
            (dataSource, name) => dataSource.Name == name,
            "dataSources");

        var filtered = EnumerableExtensions.GetFilteredConfigurationObjectList(
            dataSources,
            new[] { "dep-b" },
            (dataSource, name) => dataSource.Name == name,
            "dataSources");

        Assert.Multiple(() =>
        {
            Assert.That(empty, Is.Empty);
            Assert.That(filtered.Select(item => item.Name), Is.EqualTo(new[] { "dep-b" }));
            Assert.Throws<ArgumentException>(() =>
                EnumerableExtensions.GetFilteredConfigurationObjectList(
                    dataSources,
                    new[] { "missing" },
                    (dataSource, name) => dataSource.Name == name,
                    "dataSources"));
        });
    }

    [Test]
    public void NameAndRegexFilters_MatchExpectedObjects()
    {
        var dataSource = new DataSource { Name = "source-a" };
        var session = new SessionData { Name = "session-1" };

        Assert.Multiple(() =>
        {
            Assert.That(NameFilters.DataSource(dataSource, "source-a"), Is.True);
            Assert.That(NameFilters.SessionData(session, "session-1"), Is.True);
            Assert.That(RegexFilters.DataSource(dataSource, "^source-"), Is.True);
            Assert.That(RegexFilters.SessionData(session, "^session-\\d+$"), Is.True);
        });
    }

    [Test]
    public void DataSourceBuilder_UsesConfiguredGeneratorName_AndSerializesPatterns()
    {
        var builder = new DataSourceBuilder()
            .Named("source-a")
            .HookNamed("generator-a")
            .AddDataSourcePattern("^dep-")
            .Configure(new { Existing = "value" })
            .UpsertConfiguration(new { Added = "new" })
            .WithSerializer(new SerializeConfig { Serializer = SerializationType.Json });
        var registered = builder.Register();
        var generators = new Dictionary<string, IGenerator>
        {
            ["generator-a"] = new BuilderGenerator()
        };
        var dataSources = new[]
        {
            new DataSource { Name = "dep-1" },
            new DataSource { Name = "other" },
            registered
        };
        var yaml = new SerializerBuilder().Build().Serialize(builder);

        var built = builder.Build(Globals.GetContextWithMetadata(), dataSources, generators);

        Assert.Multiple(() =>
        {
            Assert.That(built.Generator, Is.SameAs(generators["generator-a"]));
            Assert.That(built.DataSourceList.Select(source => source.Name), Is.EqualTo(new[] { "dep-1" }));
            Assert.That(builder.ReadConfiguration()["Existing"], Is.EqualTo("value"));
            Assert.That(builder.ReadConfiguration()["Added"], Is.EqualTo("new"));
            Assert.That(yaml, Does.Contain("DataSourcePatterns"));
            Assert.That(yaml, Does.Contain("^dep-"));
            Assert.Throws<NotSupportedException>(() => builder.Read(null!, typeof(object), null!));
        });
    }

    [Test]
    public void BindFromContext_WhenConfigurationIsInvalid_AddsValidationResults()
    {
        var context = new Context
        {
            Logger = NullLogger.Instance,
            RootConfiguration = new ConfigurationBuilder().AddInMemoryCollection().Build(),
            CurrentRunningSessions = new RunningSessions(new Dictionary<string, RunningSessionData<object, object>>())
        };
        var validationResults = new List<ValidationResult>();

        var bound = Bind.BindFromContext<RequiredContextConfig>(context, validationResults, new BinderOptions
        {
            ErrorOnUnknownConfiguration = true,
            BindNonPublicProperties = false
        });

        Assert.Multiple(() =>
        {
            Assert.That(bound.Name, Is.Null);
            Assert.That(validationResults, Has.Count.EqualTo(1));
            Assert.That(validationResults[0].ErrorMessage, Does.Contain("required"));
        });
    }

    [Test]
    public void InternalContext_DirectCurrentRunningSessionsInitialization_IsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() =>
            _ = new InternalContext
            {
                CurrentRunningSessions = new RunningSessions(new Dictionary<string, RunningSessionData<object, object>>())
            });
    }

    [Test]
    public void Context_Variables_AreEmptyWhenMissingAndRefreshWhenRootConfigurationChanges()
    {
        var context = new Context
        {
            Logger = NullLogger.Instance,
            RootConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["root:value"] = "base"
                })
                .Build(),
            CurrentRunningSessions = new RunningSessions(new Dictionary<string, RunningSessionData<object, object>>())
        };

        var setRootConfiguration = typeof(Context).BaseType!
            .GetMethod("SetRootConfiguration", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var updatedConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["variables:rabbitmq:host"] = "localhost",
                ["variables:rabbitmq:port"] = "5672"
            })
            .Build();

        Assert.That(context.Variables.GetChildren(), Is.Empty);

        setRootConfiguration.Invoke(context, [updatedConfiguration]);

        Assert.Multiple(() =>
        {
            Assert.That(context.RootConfiguration["variables:rabbitmq:host"], Is.EqualTo("localhost"));
            Assert.That(context.Variables["rabbitmq:host"], Is.EqualTo("localhost"));
            Assert.That(context.Variables["rabbitmq:port"], Is.EqualTo("5672"));
        });
    }

    [Test]
    public void StatusCodeTransactionProcessor_UsesConfiguredStatusCode()
    {
        var processor = new StatusCodeTransactionProcessor
        {
            Context = new Context
            {
                Logger = NullLogger.Instance,
                RootConfiguration = new ConfigurationBuilder().Build(),
                CurrentRunningSessions = new RunningSessions(new Dictionary<string, RunningSessionData<object, object>>())
            },
            Configuration = new StatusCodeConfiguration { StatusCode = 204 }
        };

        var result = processor.Process([], new Data<object> { Body = "ignored" });

        Assert.That(result.MetaData?.Http?.StatusCode, Is.EqualTo(204));
        Assert.That(result.Body, Is.Null);
    }

    [Test]
    public void AssertionAttachment_ActionFailure_And_Reason_ExposeConfiguredValues()
    {
        var attachment = new AssertionAttachment
        {
            Path = "artifacts/out.json",
            Data = new { Name = "payload" },
            SerializationType = SerializationType.Json
        };
        var failure = new ActionFailure
        {
            Name = "send",
            Action = "POST",
            ActionType = "http",
            Reason = new Reason
            {
                Message = "failed",
                Description = "timeout"
            }
        };

        Assert.Multiple(() =>
        {
            Assert.That(attachment.Path, Is.EqualTo("artifacts/out.json"));
            Assert.That(attachment.SerializationType, Is.EqualTo(SerializationType.Json));
            Assert.That(failure.Name, Is.EqualTo("send"));
            Assert.That(failure.Action, Is.EqualTo("POST"));
            Assert.That(failure.ActionType, Is.EqualTo("http"));
            Assert.That(failure.Reason.Message, Is.EqualTo("failed"));
            Assert.That(failure.Reason.Description, Is.EqualTo("timeout"));
        });
    }
}
