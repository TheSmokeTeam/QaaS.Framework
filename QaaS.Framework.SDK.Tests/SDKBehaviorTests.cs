using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Hooks.Assertion;
using QaaS.Framework.SDK.Hooks.Generator;
using QaaS.Framework.SDK.Hooks.Probe;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.MockerObjects;
using QaaS.Framework.SDK.Session;
using QaaS.Framework.SDK.Session.CommunicationDataObjects;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects.RunningSessionsObjects;
using QaaS.Framework.Serialization;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace QaaS.Framework.SDK.Tests;

[TestFixture]
public class SDKBehaviorTests
{
    private sealed class SamplePayload
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class HookConfig
    {
        [Required]
        public string? RequiredValue { get; set; }
    }

    private sealed class CaptureLogger : Microsoft.Extensions.Logging.ILogger
    {
        public List<LogLevel> Levels { get; } = [];
        public List<object?> Scopes { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            Scopes.Add(state);
            return new Disposable();
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Levels.Add(logLevel);
        }

        private sealed class Disposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class TestSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private sealed class StaticGenerator(IEnumerable<Data<object>> data) : BaseGenerator<HookConfig>
    {
        public int Calls { get; private set; }

        public override IEnumerable<Data<object>> Generate(IImmutableList<SessionData> sessionDataList,
            IImmutableList<DataSource> dataSourceList)
        {
            Calls++;
            return data;
        }
    }

    private sealed class TestAssertion : BaseAssertion<HookConfig>
    {
        public override bool Assert(IImmutableList<SessionData> sessionDataList,
            IImmutableList<DataSource> dataSourceList) => true;
    }

    private sealed class TestProbe : BaseProbe<HookConfig>
    {
        public override void Run(IImmutableList<SessionData> sessionDataList, IImmutableList<DataSource> dataSourceList)
        {
        }
    }

    private sealed class TestProcessor : BaseTransactionProcessor<HookConfig>
    {
        public override Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData)
            => requestData;
    }

    [Test]
    public void DataExtensions_CastAndFilterData_WorkAsExpected()
    {
        var metadata = new MetaData { IoMatchIndex = 2 };
        var objectData = new Data<object> { Body = "abc", MetaData = metadata };
        var casted = objectData.CastObjectData<string>();
        var detailed = new DetailedData<object> { Body = "x", MetaData = metadata, Timestamp = DateTime.UtcNow };
        var castedDetailed = detailed.CastObjectDetailedData<string>();
        var filtered = castedDetailed.FilterData(new DataFilter { Body = false, MetaData = false, Timestamp = false });

        Assert.Multiple(() =>
        {
            Assert.That(casted.Body, Is.EqualTo("abc"));
            Assert.That(casted.MetaData, Is.SameAs(metadata));
            Assert.That(casted.CastToObjectData().Body, Is.EqualTo("abc"));
            Assert.That(castedDetailed.Body, Is.EqualTo("x"));
            Assert.That(castedDetailed.CastToObjectDetailedData().Body, Is.EqualTo("x"));
            Assert.That(filtered.Body, Is.Null);
            Assert.That(filtered.MetaData, Is.Null);
            Assert.That(filtered.Timestamp, Is.Null);
        });
    }

    [Test]
    public void DataExtensions_InvalidCast_ThrowsInvalidCastException()
    {
        var data = new Data<object> { Body = new SamplePayload { Name = "n" } };
        var detailed = new DetailedData<object> { Body = new SamplePayload { Name = "n" } };

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidCastException>(() => data.CastObjectData<int>());
            Assert.Throws<InvalidCastException>(() => detailed.CastObjectDetailedData<int>());
        });
    }

    [Test]
    public void CommunicationDataExtensions_GetByNameCastAndIoMatch_Work()
    {
        var communicationData = new CommunicationData<object>
        {
            Name = "input-a",
            Data =
            [
                new DetailedData<object> { Body = "x", MetaData = new MetaData { IoMatchIndex = 5 } }
            ]
        };

        var found = new[] { communicationData }.GetCommunicationDataByName("input-a", "Inputs");
        var casted = communicationData.CastCommunicationData<string>("Inputs");
        var ioMatchData = casted.GetDataByIoMatchIndex(5);

        Assert.Multiple(() =>
        {
            Assert.That(found.Name, Is.EqualTo("input-a"));
            Assert.That(casted.Data.Single().Body, Is.EqualTo("x"));
            Assert.That(ioMatchData.Body, Is.EqualTo("x"));
            Assert.Throws<ArgumentException>(() =>
                new[] { communicationData }.GetCommunicationDataByName("missing"));
            Assert.Throws<ArgumentException>(() =>
                new[] { communicationData, communicationData }.GetCommunicationDataByName("input-a"));
            Assert.Throws<InvalidCastException>(() => communicationData.CastCommunicationData<int>());
            Assert.Throws<ArgumentException>(() => casted.GetDataByIoMatchIndex(1234));
        });
    }

    [Test]
    public void SessionDataExtensions_GetInputOutputAndTryMethods_Work()
    {
        var session = new GenericSessionData<string, int>
        {
            Name = "session-1",
            Inputs = [new CommunicationData<string> { Name = "in", Data = [new DetailedData<string> { Body = "v" }] }],
            Outputs = [new CommunicationData<int> { Name = "out", Data = [new DetailedData<int> { Body = 7 }] }]
        };

        var foundSession = new[] { session }.GetSessionDataByName("session-1");
        var input = session.GetInputByName<string, int>("in");
        var output = session.GetOutputByName<string, int>("out");
        var foundInput = session.TryGetInputByName<string, int>("in", out var inputOut);
        var foundOutput = session.TryGetOutputByName<string, int>("out", out var outputOut);
        var missingInput = session.TryGetInputByName<string, int>("missing", out var missingIn);
        var missingOutput = session.TryGetOutputByName<string, int>("missing", out var missingOut);

        Assert.Multiple(() =>
        {
            Assert.That(foundSession.Name, Is.EqualTo("session-1"));
            Assert.That(input.Data.Single().Body, Is.EqualTo("v"));
            Assert.That(output.Data.Single().Body, Is.EqualTo(7));
            Assert.That(foundInput, Is.True);
            Assert.That(inputOut, Is.Not.Null);
            Assert.That(foundOutput, Is.True);
            Assert.That(outputOut, Is.Not.Null);
            Assert.That(missingInput, Is.False);
            Assert.That(missingIn, Is.Null);
            Assert.That(missingOutput, Is.False);
            Assert.That(missingOut, Is.Null);
            Assert.Throws<ArgumentException>(() =>
                new[] { session, session }.GetSessionDataByName("session-1"));
        });
    }

    [Test]
    public void DataSourceExtensions_GetByNameAndRetrieveAndCast_Work()
    {
        var generator = new StaticGenerator(
        [
            new Data<object> { Body = "a" },
            new Data<object> { Body = "b" }
        ]);
        var dataSource = new DataSource
        {
            Name = "source-a",
            Lazy = true,
            DataSourceList = [],
            Generator = generator
        };

        var found = new[] { dataSource }.GetDataSourceByName("source-a");
        var retrieved = dataSource.RetrieveAndCast<string>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(found.Name, Is.EqualTo("source-a"));
            Assert.That(retrieved.Select(item => item.Body), Is.EqualTo(new[] { "a", "b" }));
            Assert.Throws<ArgumentException>(() => new[] { dataSource }.GetDataSourceByName("missing"));
            Assert.Throws<ArgumentException>(() => new[] { dataSource, dataSource }.GetDataSourceByName("source-a"));
            Assert.Throws<InvalidCastException>(() => dataSource.RetrieveAndCast<int>().ToList());
        });
    }

    [Test]
    public void DataSource_Retrieve_CachesWhenNotLazy_AndSupportsSerializerDeserializer()
    {
        var serializerGenerator = new StaticGenerator([new Data<object> { Body = "payload" }]);
        var serializingDataSource = new DataSource
        {
            Name = "serializer",
            Lazy = false,
            DataSourceList = [],
            Generator = serializerGenerator,
            Serializer = new QaaS.Framework.Serialization.Serializers.Json()
        };

        var first = serializingDataSource.Retrieve().ToList();
        var second = serializingDataSource.Retrieve().ToList();

        var jsonBytes = new QaaS.Framework.Serialization.Serializers.Json()
            .Serialize(new SamplePayload { Name = "from-json" });
        var deserializingDataSource = new DataSource
        {
            Name = "deserializer",
            Lazy = true,
            DataSourceList = [],
            Generator = new StaticGenerator([new Data<object> { Body = jsonBytes }]),
            Deserializer = new QaaS.Framework.Serialization.Deserializers.Json(),
            DeserializerSpecificType = typeof(SamplePayload)
        };

        var deserialized = deserializingDataSource.Retrieve().Single();

        Assert.Multiple(() =>
        {
            Assert.That(serializerGenerator.Calls, Is.EqualTo(1));
            Assert.That(first.Single().Body, Is.InstanceOf<byte[]>());
            Assert.That(second.Single().Body, Is.InstanceOf<byte[]>());
            Assert.That(deserialized.Body, Is.InstanceOf<SamplePayload>());
            Assert.That(((SamplePayload)deserialized.Body!).Name, Is.EqualTo("from-json"));
        });
    }

    [Test]
    public void DataSource_InvalidSerializerDeserializerCombinations_Throw()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => _ = new DataSource
            {
                Name = "invalid-meta",
                Lazy = true,
                DataSourceList = [],
                Generator = new StaticGenerator([]),
                Serializer = new QaaS.Framework.Serialization.Serializers.Json(),
                Deserializer = new QaaS.Framework.Serialization.Deserializers.Json()
            });

            Assert.Throws<InvalidOperationException>(() => _ = new DataSource
            {
                Name = "invalid-specific",
                Lazy = true,
                DataSourceList = [],
                Generator = new StaticGenerator([]),
                DeserializerSpecificType = typeof(SamplePayload)
            });
        });
    }

    [Test]
    public void DataSource_DeserializeNonByteArray_ThrowsInvalidOperationException()
    {
        var dataSource = new DataSource
        {
            Name = "invalid-bytes",
            Lazy = true,
            DataSourceList = [],
            Generator = new StaticGenerator([new Data<object> { Body = "not-bytes" }]),
            Deserializer = new QaaS.Framework.Serialization.Deserializers.Json()
        };

        Assert.Throws<InvalidOperationException>(() => dataSource.Retrieve().ToList());
    }

    [Test]
    public void SessionDataSerialization_RoundTripsJsonBasedSessionData()
    {
        var now = DateTime.UtcNow;
        var session = new SessionData
        {
            Name = "session-a",
            UtcStartTime = now.AddSeconds(-1),
            UtcEndTime = now,
            Inputs =
            [
                new CommunicationData<object>
                {
                    Name = "input",
                    SerializationType = SerializationType.Json,
                    Data =
                    [
                        new DetailedData<object>
                        {
                            Body = new SamplePayload { Name = "x" },
                            Timestamp = now,
                            MetaData = new MetaData { IoMatchIndex = 10 }
                        }
                    ]
                }
            ],
            Outputs = []
        };

        var serialized = SessionDataSerialization.SerializeSessionData(session);
        var deserialized = SessionDataSerialization.DeserializeSessionData(serialized);
        var payload = deserialized.Inputs!.Single().Data.Single().Body as SamplePayload;

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.Name, Is.EqualTo("session-a"));
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload!.Name, Is.EqualTo("x"));
            Assert.That(deserialized.Inputs!.Single().Data.Single().MetaData?.IoMatchIndex, Is.EqualTo(10));
        });
    }

    [Test]
    public void SessionDataSerialization_RawBytePathWithoutSerializer_Works()
    {
        var rawBytes = new byte[] { 1, 2, 3 };
        var communicationData = new CommunicationData<object>
        {
            Name = "raw",
            SerializationType = null,
            Data = [new DetailedData<object> { Body = rawBytes }]
        };

        var serializedCommunication = SessionDataSerialization.SerializeCommunicationData(communicationData);
        var deserializedCommunication = SessionDataSerialization.DeserializeCommunicationData(serializedCommunication);

        Assert.That(deserializedCommunication.Data.Single().Body, Is.EqualTo(rawBytes));
    }

    [Test]
    public void MetaData_AndDataFilter_DefaultsAndGuards_Work()
    {
        var defaults = new DataFilter();

        Assert.Multiple(() =>
        {
            Assert.That(defaults.Body, Is.True);
            Assert.That(defaults.Timestamp, Is.True);
            Assert.That(defaults.MetaData, Is.True);
            Assert.Throws<InvalidOperationException>(() => _ = new MetaData
            {
                Serializer = new QaaS.Framework.Serialization.Serializers.Json(),
                Deserializer = new QaaS.Framework.Serialization.Deserializers.Json()
            });
        });
    }

    [Test]
    public void RunningCommunicationData_GetData_StreamsQueuedValues()
    {
        var running = new RunningCommunicationData<string> { Name = "stream" };
        running.Queue.Enqueue(new DetailedData<string> { Body = "first" });
        running.Queue.Enqueue(new DetailedData<string> { Body = "second" });
        running.Data.Add(new DetailedData<string> { Body = "trigger" });
        running.Data.CompleteAdding();

        var values = running.GetData().Select(data => data.Body).ToList();

        Assert.That(values, Is.EqualTo(new[] { "first", "second" }));
    }

    [Test]
    public void RunningExtensions_GetByName_Work()
    {
        var runningIn = new RunningCommunicationData<string> { Name = "in" };
        var runningOut = new RunningCommunicationData<int> { Name = "out" };
        var runningSession = new RunningSessionData<string, int>
        {
            Inputs = [runningIn],
            Outputs = [runningOut]
        };

        Assert.Multiple(() =>
        {
            Assert.That(new[] { runningIn }.GetRunningCommunicationDataByName("in"), Is.SameAs(runningIn));
            Assert.That(runningSession.GetInputByName<string, int>("in"), Is.SameAs(runningIn));
            Assert.That(runningSession.GetOutputByName<string, int>("out"), Is.SameAs(runningOut));
            Assert.Throws<ArgumentException>(() =>
                new[] { runningIn, runningIn }.GetRunningCommunicationDataByName("in"));
        });
    }

    [Test]
    public void RunningSessions_GetSessionByName_AndGetAllSessions_Work()
    {
        var session = new RunningSessionData<object, object>();
        var runningSessions = new RunningSessions(new Dictionary<string, RunningSessionData<object, object>>
        {
            ["s1"] = session
        });

        Assert.Multiple(() =>
        {
            Assert.That(runningSessions.GetAllSessions(), Has.Count.EqualTo(1));
            Assert.That(runningSessions.GetSessionByName("s1"), Is.SameAs(session));
            Assert.Throws<ArgumentException>(() => runningSessions.GetSessionByName("missing"));
        });
    }

    [Test]
    public void CommunicationMethods_BuildExpectedChannelAndEndpointNames()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CommunicationMethods.CreateChannelRunnerToMocker("json", "Srv", "A"),
                Is.EqualTo("runner-to-mocker:json:srv:a"));
            Assert.That(CommunicationMethods.CreateChannelMockerToRunner("xml", "Srv", "B"),
                Is.EqualTo("mocker-to-runner:xml:srv:b"));
            Assert.That(CommunicationMethods.CreateConsumerEndpointInput("Server"), Is.EqualTo("server:input"));
            Assert.That(CommunicationMethods.CreateConsumerEndpointOutput("Server"), Is.EqualTo("server:output"));
        });
    }

    [Test]
    public void Context_GlobalDictionary_InsertAndGet_Work()
    {
        var context = new Context
        {
            Logger = NullLogger.Instance,
            RootConfiguration = new ConfigurationBuilder().Build(),
            CurrentRunningSessions = new RunningSessions(new Dictionary<string, RunningSessionData<object, object>>())
        };

        context.InsertValueIntoGlobalDictionary(["root", "child", "leaf"], 17);
        var fetched = context.GetValueFromGlobalDictionary(["root", "child", "leaf"]);

        Assert.Multiple(() =>
        {
            Assert.That(fetched, Is.EqualTo(17));
            Assert.Throws<ArgumentException>(() => context.InsertValueIntoGlobalDictionary([], 1));
            Assert.Throws<ArgumentException>(() => context.GetValueFromGlobalDictionary([]));
            Assert.Throws<KeyNotFoundException>(() => context.GetValueFromGlobalDictionary(["missing"]));
        });
    }

    [Test]
    public void ContextBuilder_BuildInternal_UsesOverwritesAndMetadata()
    {
        var baseYaml = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.yaml");
        var overwriteYaml = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.yaml");
        var caseYaml = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.yaml");
        File.WriteAllText(baseYaml, "root:\n  value: base\n");
        File.WriteAllText(overwriteYaml, "root:\n  value: overwrite\n");
        File.WriteAllText(caseYaml, "root:\n  value: case\n");

        try
        {
            var runningSessions = new RunningSessions(new Dictionary<string, RunningSessionData<object, object>>());
            var context = new ContextBuilder(baseYaml)
                .SetLogger(NullLogger.Instance)
                .WithOverwriteFile(overwriteYaml)
                .WithOverwriteArgument("root:value=argument")
                .SetCase(caseYaml)
                .SetExecutionId("exec-1")
                .SetCurrentRunningSessions(runningSessions)
                .WithEnvironmentVariableResolution()
                .ResolveCaseLast()
                .BuildInternal();

            Assert.Multiple(() =>
            {
                Assert.That(context.ExecutionId, Is.EqualTo("exec-1"));
                Assert.That(context.CaseName, Is.EqualTo(caseYaml));
                Assert.That(context.RootConfiguration["root:value"], Is.EqualTo("case"));
                Assert.That(context.InternalRunningSessions, Is.SameAs(runningSessions));
            });
        }
        finally
        {
            File.Delete(baseYaml);
            File.Delete(overwriteYaml);
            File.Delete(caseYaml);
        }
    }

    [Test]
    public void Bind_BindsFromContextAndReturnsValidationResults()
    {
        var context = new Context
        {
            Logger = NullLogger.Instance,
            RootConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RequiredValue"] = "ok"
                })
                .Build(),
            CurrentRunningSessions = new RunningSessions(new Dictionary<string, RunningSessionData<object, object>>())
        };
        var validation = new List<ValidationResult>();

        var bound = Bind.BindFromContext<HookConfig>(context, validation, new BinderOptions
        {
            ErrorOnUnknownConfiguration = true,
            BindNonPublicProperties = false
        });

        Assert.Multiple(() =>
        {
            Assert.That(bound.RequiredValue, Is.EqualTo("ok"));
            Assert.That(validation, Is.Empty);
            Assert.That(context.RootConfiguration["RequiredValue"], Is.EqualTo("ok"));
        });
    }

    [Test]
    public void BaseHooks_LoadAndValidateConfiguration_Work()
    {
        var hookConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RequiredValue"] = "configured"
            })
            .Build();

        var context = new Context
        {
            Logger = NullLogger.Instance,
            RootConfiguration = hookConfiguration,
            CurrentRunningSessions = new RunningSessions(new Dictionary<string, RunningSessionData<object, object>>())
        };

        var assertion = new TestAssertion { Context = context };
        var probe = new TestProbe { Context = context };
        var processor = new TestProcessor { Context = context };
        var generator = new StaticGenerator([]) { Context = context };

        Assert.Multiple(() =>
        {
            Assert.That(assertion.LoadAndValidateConfiguration(hookConfiguration), Is.Empty);
            Assert.That(assertion.Configuration.RequiredValue, Is.EqualTo("configured"));
            Assert.That(probe.LoadAndValidateConfiguration(hookConfiguration), Is.Empty);
            Assert.That(processor.LoadAndValidateConfiguration(hookConfiguration), Is.Empty);
            Assert.That(generator.LoadAndValidateConfiguration(hookConfiguration), Is.Empty);
            Assert.That(generator.Generate([], []), Is.Empty);
            Assert.That(processor.Process([], new Data<object> { Body = "x" }).Body, Is.EqualTo("x"));
        });
    }

    [Test]
    public void SerilogExtensions_AddEnrichmentAndMetadataLogs()
    {
        var originalEnv = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
        try
        {
            var sink = new TestSink();
            Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST", null);
            var logger = new LoggerConfiguration()
                .Enrich.WithHostname()
                .Enrich.WithEnvironment()
                .WriteTo.Sink(sink)
                .CreateLogger();
            logger.Information("test");

            Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST", "1");
            var ciSink = new TestSink();
            var ciLogger = new LoggerConfiguration()
                .Enrich.WithEnvironment()
                .WriteTo.Sink(ciSink)
                .CreateLogger();
            ciLogger.Information("ci");

            Assert.Multiple(() =>
            {
                Assert.That(sink.Events, Has.Count.EqualTo(1));
                Assert.That(sink.Events[0].Properties.ContainsKey("Hostname"), Is.True);
                Assert.That(sink.Events[0].Properties["Environment"].ToString(), Is.EqualTo("\"Local\""));
                Assert.That(ciSink.Events[0].Properties["Environment"].ToString(), Is.EqualTo("\"CI\""));
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST", originalEnv);
        }
    }

    [Test]
    public void SerilogExtensions_MetadataLoggingMethods_WriteExpectedLevels()
    {
        var logger = new CaptureLogger();
        var metadata = new { Id = 7, Name = "meta" };

        logger.LogInformationWithMetaData("info {Value}", metadata, 1);
        logger.LogWarningWithMetaData("warn {Value}", metadata, 2);
        logger.LogErrorWithMetaData("error {Value}", metadata, 3);
        logger.LogCriticalWithMetaData("critical {Value}", metadata, 4);
        logger.LogDebugWithMetaData("debug {Value}", metadata, 5);
        logger.LogTraceWithMetaData("trace {Value}", metadata, 6);

        Assert.Multiple(() =>
        {
            Assert.That(logger.Levels, Is.EqualTo(new[]
            {
                LogLevel.Information,
                LogLevel.Warning,
                LogLevel.Error,
                LogLevel.Critical,
                LogLevel.Debug,
                LogLevel.Trace
            }));
            Assert.That(logger.Scopes.Count, Is.EqualTo(6));
        });
    }
}
