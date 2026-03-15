using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Session;
using QaaS.Framework.SDK.Session.CommunicationDataObjects;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects.RunningSessionsObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.SDK.Tests;

[TestFixture]
public class SDKSerializationCoverageTests
{
    private sealed class JsonPayload
    {
        public string Value { get; set; } = string.Empty;
    }

    [Test]
    public void SessionDataSerialization_SerializeAndDeserializeCommunicationData_CoversTypedAndRawBranches()
    {
        var serialized = SessionDataSerialization.SerializeCommunicationData(new CommunicationData<object>
        {
            Name = "json",
            SerializationType = SerializationType.Json,
            Data =
            [
                new DetailedData<object>
                {
                    Body = new JsonPayload { Value = "payload" },
                    MetaData = new MetaData { IoMatchIndex = 3 },
                    Timestamp = DateTime.UtcNow
                }
            ]
        });
        var deserialized = SessionDataSerialization.DeserializeCommunicationData(serialized);

        var raw = SessionDataSerialization.DeserializeCommunicationData(new SerializedCommunicationData
        {
            Name = "raw",
            SerializationType = null,
            Data =
            [
                new SerializedDetailedData
                {
                    Body = new byte[] { 1, 2, 3 }
                }
            ]
        });
        var jsonNodeFallback = SessionDataSerialization.DeserializeCommunicationData(new SerializedCommunicationData
        {
            Name = "json-node",
            SerializationType = SerializationType.Json,
            Data =
            [
                new SerializedDetailedData
                {
                    Body = new QaaS.Framework.Serialization.Serializers.Json().Serialize(new { value = "x" }),
                    Type = null
                }
            ]
        });

        Assert.Multiple(() =>
        {
            Assert.That(serialized.Data.Single().Type, Is.Not.Null);
            Assert.That(deserialized.Data.Single().Body, Is.InstanceOf<JsonPayload>());
            Assert.That(((JsonPayload)deserialized.Data.Single().Body!).Value, Is.EqualTo("payload"));
            Assert.That(raw.Data.Single().Body, Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(jsonNodeFallback.Data.Single().Body, Is.Not.Null);
        });
    }

    [Test]
    public void SessionDataSerialization_DeserializeSessionData_ThrowsForNullPayload()
    {
        Assert.Throws<ArgumentException>(() => SessionDataSerialization.DeserializeSessionData("null"u8.ToArray()));
    }

    [Test]
    public void SessionDataSerialization_SerializeAndDeserializeSessionData_CoversNullCollectionsAndNullBodyTypes()
    {
        var session = new SessionData
        {
            Name = "session",
            Inputs = null,
            Outputs = null
        };
        var serialized = SessionDataSerialization.SerializeSessionData(session);
        var deserialized = SessionDataSerialization.DeserializeSessionData(serialized);

        var serializedCommunication = SessionDataSerialization.SerializeCommunicationData(new CommunicationData<object>
        {
            Name = "nullable-json",
            SerializationType = SerializationType.Json,
            Data =
            [
                new DetailedData<object>
                {
                    Body = null,
                    Timestamp = DateTime.UtcNow
                }
            ]
        });

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.Name, Is.EqualTo("session"));
            Assert.That(deserialized.Inputs, Is.Null);
            Assert.That(deserialized.Outputs, Is.Null);
            Assert.That(serializedCommunication.Data.Single().Type, Is.Null);
        });
    }

    [Test]
    public void SessionDataExtensions_ThrowWhenSourceSessionOrCollectionIsMissing()
    {
        GenericSessionData<string, int>? missingSession = null;

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() =>
                SessionDataExtensions.GetSessionDataByName<string, int>(null, "missing"));
            Assert.Throws<ArgumentException>(() =>
                missingSession.GetInputByName<string, int>("input"));
            Assert.Throws<ArgumentException>(() =>
                missingSession.GetOutputByName<string, int>("output"));
        });
    }

    [Test]
    public void DataExtensions_CastFailuresAndFilterBranches_AreCovered()
    {
        var invalidData = new Data<object> { Body = "text" };
        var invalidDetailedData = new DetailedData<object> { Body = "text" };
        var filtered = new DetailedData<string>
        {
            Body = "payload",
            Timestamp = DateTime.UtcNow,
            MetaData = new MetaData { IoMatchIndex = 1 }
        }.FilterData(new DataFilter
        {
            Body = false,
            Timestamp = false,
            MetaData = false
        });

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidCastException>(() => invalidData.CastObjectData<byte[]>());
            Assert.Throws<InvalidCastException>(() => invalidDetailedData.CastObjectDetailedData<byte[]>());
            Assert.That(filtered.Body, Is.Null);
            Assert.That(filtered.Timestamp, Is.Null);
            Assert.That(filtered.MetaData, Is.Null);
        });
    }

    [Test]
    public void DataAndContextExtensions_CoverSuccessfulCastsMetadataPathsAndRunningCommunicationLookup()
    {
        var metaData = new MetaData { IoMatchIndex = 7 };
        var timestamp = DateTime.UtcNow;
        var castData = new Data<object> { Body = "value", MetaData = metaData }.CastObjectData<string>();
        var castDetailed = new DetailedData<object>
        {
            Body = "detail",
            MetaData = metaData,
            Timestamp = timestamp
        }.CastObjectDetailedData<string>();
        var objectData = new Data<string> { Body = "value", MetaData = metaData }.CastToObjectData();
        var objectDetailed = new DetailedData<string>
        {
            Body = "detail",
            MetaData = metaData,
            Timestamp = timestamp
        }.CastToObjectDetailedData();
        var filtered = new DetailedData<string>
        {
            Body = "payload",
            MetaData = metaData,
            Timestamp = timestamp
        }.FilterData(new DataFilter
        {
            Body = true,
            Timestamp = true,
            MetaData = true
        });

        var running = new RunningCommunicationData<string> { Name = "input" };
        var duplicateItems = new[]
        {
            running,
            new RunningCommunicationData<string> { Name = "input" }
        };
        var caseContext = new InternalContext
        {
            Logger = NullLogger.Instance,
            RootConfiguration = new ConfigurationBuilder().Build(),
            InternalRunningSessions = new RunningSessions(new Dictionary<string, RunningSessionData<object, object>>()),
            CaseName = "case-a",
            ExecutionId = "exec-1"
        };
        caseContext.InsertValueIntoGlobalDictionary(["case-a", "exec-1", nameof(MetaDataConfig)], new MetaDataConfig());
        var rootContext = new InternalContext
        {
            Logger = NullLogger.Instance,
            RootConfiguration = new ConfigurationBuilder().Build(),
            InternalRunningSessions = new RunningSessions(new Dictionary<string, RunningSessionData<object, object>>())
        };
        rootContext.InsertValueIntoGlobalDictionary([nameof(MetaDataConfig)], new MetaDataConfig());
        var missingContext = new InternalContext
        {
            Logger = NullLogger.Instance,
            RootConfiguration = new ConfigurationBuilder().Build(),
            InternalRunningSessions = new RunningSessions(new Dictionary<string, RunningSessionData<object, object>>())
        };

        Assert.Multiple(() =>
        {
            Assert.That(castData.Body, Is.EqualTo("value"));
            Assert.That(castDetailed.Body, Is.EqualTo("detail"));
            Assert.That(objectData.Body, Is.EqualTo("value"));
            Assert.That(objectDetailed.Timestamp, Is.EqualTo(timestamp));
            Assert.That(filtered.Body, Is.EqualTo("payload"));
            Assert.That(filtered.Timestamp, Is.EqualTo(timestamp));
            Assert.That(filtered.MetaData, Is.SameAs(metaData));
            Assert.That(new[] { running }.GetRunningCommunicationDataByName("input"), Is.SameAs(running));
            Assert.That(caseContext.GetMetaDataPath(), Is.EqualTo(new[] { "case-a", "exec-1", nameof(MetaDataConfig) }));
            Assert.That(rootContext.GetMetaDataPath(), Is.EqualTo(new[] { nameof(MetaDataConfig) }));
            Assert.That(caseContext.GetMetaDataFromContext(), Is.TypeOf<MetaDataConfig>());
            Assert.That(rootContext.GetMetaDataFromContext(), Is.TypeOf<MetaDataConfig>());
            Assert.Throws<ArgumentException>(() =>
                Array.Empty<RunningCommunicationData<string>>().GetRunningCommunicationDataByName("missing"));
            Assert.Throws<ArgumentException>(() =>
                duplicateItems.GetRunningCommunicationDataByName("input", "Inputs"));
            Assert.Throws<KeyNotFoundException>(() => missingContext.GetMetaDataFromContext());
        });
    }

    [Test]
    public void SpecificTypeConfig_And_MessagePackDeserializer_CoverFallbackBranches()
    {
        var entryAssembly = Assembly.GetEntryAssembly()!;
        var entryAssemblyType = entryAssembly.GetTypes().First(type => type.FullName is not null);
        var configuredType = new SpecificTypeConfig
        {
            AssemblyName = null,
            TypeFullName = entryAssemblyType.FullName
        }.GetConfiguredType();
        var explicitAssemblyType = new SpecificTypeConfig
        {
            AssemblyName = typeof(JsonPayload).Assembly.FullName,
            TypeFullName = typeof(JsonPayload).FullName
        }.GetConfiguredType();
        var deserializer = new QaaS.Framework.Serialization.Deserializers.MessagePack();
        var serialized = new QaaS.Framework.Serialization.Serializers.MessagePack().Serialize("value");

        Assert.Multiple(() =>
        {
            Assert.That(configuredType, Is.EqualTo(entryAssemblyType));
            Assert.That(explicitAssemblyType, Is.EqualTo(typeof(JsonPayload)));
            Assert.That(deserializer.Deserialize(null, typeof(string)), Is.Null);
            Assert.That(deserializer.Deserialize(serialized, typeof(string)), Is.EqualTo("value"));
            Assert.That(deserializer.Deserialize(serialized), Is.Not.Null);
        });
    }

    [Test]
    public void ContextBuilder_Build_WithoutResolveCaseLast_CoversLegacyBuildPath()
    {
        var baseYaml = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.yaml");
        File.WriteAllText(baseYaml, "root:\n  value: base\n");

        try
        {
            var builder = new ContextBuilder(baseYaml)
                .SetLogger(NullLogger.Instance)
                .SetConfigurationFile(null)
                .WithOverwriteFile(null)
                .WithOverwriteArgument(null)
                .SetCase(null)
                .WithOverwriteArgument("root:value=argument")
                .SetExecutionId("legacy");

#pragma warning disable CS0618
            var context = builder.Build();
#pragma warning restore CS0618

            Assert.Multiple(() =>
            {
                Assert.That(context.ExecutionId, Is.EqualTo("legacy"));
                Assert.That(context.RootConfiguration["root:value"], Is.EqualTo("argument"));
            });
        }
        finally
        {
            File.Delete(baseYaml);
        }
    }
}
