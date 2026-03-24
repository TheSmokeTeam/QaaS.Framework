using System.Collections.Immutable;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Amazon.S3;
using Amazon.S3.Model;
using IBM.WMQ;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using QaaS.Framework.Protocols.ConfigurationObjects;
using MongoDB.Bson;
using MongoDB.Driver;
using QaaS.Framework.Protocols.ConfigurationObjects.IbmMq;
using QaaS.Framework.Protocols.ConfigurationObjects.MongoDb;
using QaaS.Framework.Protocols.ConfigurationObjects.Redis;
using QaaS.Framework.Protocols.ConfigurationObjects.S3;
using QaaS.Framework.Protocols.ConfigurationObjects.Socket;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.Protocols.Utils.S3Utils;
using QaaS.Framework.SDK.Session;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using StackExchange.Redis;
using Trino.Data.ADO.Server;
using StringValue = Google.Protobuf.WellKnownTypes.StringValue;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class ProtocolAdvancedBehaviorTests
{
    [Serializable]
    private sealed class SerializableS3Payload
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private sealed record S3DeserializationScenario(
        string Key,
        byte[] Payload,
        Func<byte[]?, object?> Deserialize,
        Action<object?> AssertResult);

    private sealed class FakeS3Client : IS3Client
    {
        public required IAmazonS3 Client { get; init; }
        public Func<string, string, string, Task<IEnumerable<S3Object>>>? ListObjects { get; init; }
        public Func<string, string, string, bool, IEnumerable<KeyValuePair<S3Object, byte[]?>>>? GetAllObjects { get; init; }
        public bool Disposed { get; private set; }

        public Task<IEnumerable<DeleteObjectsResponse>> EmptyS3Bucket(string bucketName, string prefix = "",
            string delimiter = "") => Task.FromResult<IEnumerable<DeleteObjectsResponse>>([]);

        public Task<IEnumerable<S3Object>> ListAllObjectsInS3Bucket(string bucketName, string prefix = "",
            string delimiter = "", bool skipEmptyObjects = true)
            => ListObjects?.Invoke(bucketName, prefix, delimiter)
               ?? Task.FromResult<IEnumerable<S3Object>>([]);

        public IEnumerable<KeyValuePair<S3Object, byte[]?>> GetAllObjectsInS3BucketUnOrdered(string bucketName,
            string prefix = "", string delimiter = "", bool skipEmptyObjects = true)
            => GetAllObjects?.Invoke(bucketName, prefix, delimiter, skipEmptyObjects) ?? [];

        public KeyValuePair<S3Object, byte[]?> GetObjectFromObjectMetadata(S3Object s3ObjectMetadata, string bucketName)
            => throw new NotImplementedException();

        public IEnumerable<PutObjectResponse> PutObjectsInS3BucketSync(string bucketName,
            IEnumerable<KeyValuePair<string, byte[]>> s3KeyValueItems) => [];

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class ControlledTimeS3Protocol : S3Protocol
    {
        private readonly DateTime _currentUtc;

        public ControlledTimeS3Protocol(S3BucketReaderConfig configuration, DataFilter dataFilter, DateTime currentUtc)
            : base(configuration, dataFilter, Globals.Logger)
        {
            _currentUtc = currentUtc;
        }

        protected override DateTime GetCurrentDateTimeUtc() => _currentUtc;
    }

    private sealed class TestSocketProtocol(SocketReaderConfig configuration) : SocketProtocol(configuration, Globals.Logger)
    {
        public Queue<byte[]> Responses { get; } = new();

        protected override Span<byte> GetMessage()
        {
            return Responses.Count > 0 ? Responses.Dequeue() : [];
        }
    }

    private sealed class TestIbmMqProtocol(IbmMqReaderConfig configuration) : IbmMqProtocol(configuration)
    {
        public MQMessage? NextMessage { get; set; }

        protected override MQMessage GetMessage(TimeSpan timeout)
        {
            return NextMessage ?? new MQMessage();
        }
    }

    private sealed class PostgreSqlProtocolWrapper(PostgreSqlReaderConfig config) : PostgreSqlProtocol(config, Globals.Logger)
    {
        public void ConfigureState(string insertionField, DateTime startTime, bool filterFromStartTime, string? where)
        {
            InsertionTimeField = insertionField;
            StartTimeDbTimeZone = startTime;
            FilterFromStartTime = filterFromStartTime;
            WhereStatement = where;
        }

        public string AscQuery() => GetTableQueryArrangedByInsertionTimeFieldAsc();
        public string PlainQuery() => GetTableQueryWithoutRegardToInsertionTimeField();
        public string LatestQuery() => GetLatestTableRowQuery();
        public string Format(DateTime time) => GetTimeFieldSqlFormat(time);
    }

    private sealed class InspectablePostgreSqlProtocol : PostgreSqlProtocol
    {
        private readonly Func<int, IDataReader> _schemaReaderFactory;
        private readonly IDataReader _resultReader;

        public InspectablePostgreSqlProtocol(PostgreSqlReaderConfig config, IDataReader schemaReader,
            IDataReader resultReader)
            : this(config, _ => schemaReader, resultReader)
        {
        }

        public InspectablePostgreSqlProtocol(PostgreSqlReaderConfig config, Func<int, IDataReader> schemaReaderFactory,
            IDataReader resultReader)
            : base(config, Globals.Logger, new NpgsqlConnection())
        {
            _schemaReaderFactory = schemaReaderFactory;
            _resultReader = resultReader;
        }

        public int SchemaReaderCalls { get; private set; }
        public bool[]? LastUnknownResultTypeList { get; private set; }

        public IDataReader InvokeExecuteReader(NpgsqlCommand command) => ExecuteReader(command);

        protected override IDataReader ExecuteSchemaReader(NpgsqlCommand command)
        {
            SchemaReaderCalls++;
            return _schemaReaderFactory(SchemaReaderCalls);
        }

        protected override IDataReader ExecutePostgreSqlReader(NpgsqlCommand command)
        {
            LastUnknownResultTypeList = command.UnknownResultTypeList;
            return _resultReader;
        }
    }

    private sealed class MsSqlProtocolWrapper(MsSqlReaderConfig config) : MsSqlProtocol(config, Globals.Logger)
    {
        public void ConfigureState(string insertionField, DateTime startTime, bool filterFromStartTime, string? where)
        {
            InsertionTimeField = insertionField;
            StartTimeDbTimeZone = startTime;
            FilterFromStartTime = filterFromStartTime;
            WhereStatement = where;
        }

        public string AscQuery() => GetTableQueryArrangedByInsertionTimeFieldAsc();
        public string PlainQuery() => GetTableQueryWithoutRegardToInsertionTimeField();
        public string LatestQuery() => GetLatestTableRowQuery();
        public string Format(DateTime time) => GetTimeFieldSqlFormat(time);
    }

    private sealed class OracleSqlProtocolWrapper(OracleReaderConfig config) : OracleSqlProtocol(config, Globals.Logger)
    {
        public void ConfigureState(string insertionField, DateTime startTime, bool filterFromStartTime, string? where)
        {
            InsertionTimeField = insertionField;
            StartTimeDbTimeZone = startTime;
            FilterFromStartTime = filterFromStartTime;
            WhereStatement = where;
        }

        public string AscQuery() => GetTableQueryArrangedByInsertionTimeFieldAsc();
        public string PlainQuery() => GetTableQueryWithoutRegardToInsertionTimeField();
        public string LatestQuery() => GetLatestTableRowQuery();
        public string Format(DateTime time) => GetTimeFieldSqlFormat(time);
    }

    private sealed class TrinoSqlProtocolWrapper(TrinoReaderConfig config) : TrinoSqlProtocol(config, Globals.Logger)
    {
        public void ConfigureState(string insertionField, DateTime startTime, bool filterFromStartTime, string? where)
        {
            InsertionTimeField = insertionField;
            StartTimeDbTimeZone = startTime;
            FilterFromStartTime = filterFromStartTime;
            WhereStatement = where;
        }

        public string AscQuery() => GetTableQueryArrangedByInsertionTimeFieldAsc();
        public string PlainQuery() => GetTableQueryWithoutRegardToInsertionTimeField();
        public string LatestQuery() => GetLatestTableRowQuery();
        public string Format(DateTime time) => GetTimeFieldSqlFormat(time);
    }

    [Test]
    public void S3Client_ListsAndReadsObjects()
    {
        var rawMetadataBytes = new byte[] { 0, 255, 1, 128 };
        var s3Mock = new Mock<IAmazonS3>();
        s3Mock.Setup(client => client.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(new ListObjectsV2Response
            {
                IsTruncated = false,
                S3Objects =
                [
                    new S3Object { Key = "empty", Size = 0 },
                    new S3Object { Key = "full", Size = 4 }
                ]
            });
        s3Mock.Setup(client => client.GetObjectAsync("bucket", "full", null, default))
            .ReturnsAsync(new GetObjectResponse
            {
                ResponseStream = new MemoryStream([1, 2, 3, 4])
            });
        s3Mock.Setup(client => client.GetObjectAsync("bucket", "meta", null, default))
            .ReturnsAsync(new GetObjectResponse
            {
                ResponseStream = new MemoryStream(rawMetadataBytes)
            });

        var client = new S3Client(s3Mock.Object, NullLogger.Instance, maxRetryCount: 1);

        var nonEmpty = client.ListAllObjectsInS3Bucket("bucket", skipEmptyObjects: true).Result.ToList();
        var withEmpty = client.ListAllObjectsInS3Bucket("bucket", skipEmptyObjects: false).Result.ToList();
        var allObjects = client.GetAllObjectsInS3BucketUnOrdered("bucket", skipEmptyObjects: false).ToList();
        var fromMetadata = client.GetObjectFromObjectMetadata(new S3Object { Key = "meta" }, "bucket");

        Assert.Multiple(() =>
        {
            Assert.That(nonEmpty.Select(obj => obj.Key), Is.EqualTo(["full"]));
            Assert.That(withEmpty, Has.Count.EqualTo(2));
            Assert.That(allObjects.Single(pair => pair.Key.Key == "full").Value, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            Assert.That(fromMetadata.Value, Is.EqualTo(rawMetadataBytes));
        });
    }

    [Test]
    public void S3Client_EmptyS3Bucket_DeletesObjects()
    {
        var s3Mock = new Mock<IAmazonS3>();
        s3Mock.Setup(client => client.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(new ListObjectsV2Response
            {
                IsTruncated = false,
                S3Objects = [new S3Object { Key = "k1" }]
            });
        s3Mock.Setup(client => client.DeleteObjectsAsync(It.IsAny<DeleteObjectsRequest>(), default))
            .ReturnsAsync(new DeleteObjectsResponse());

        var client = new S3Client(s3Mock.Object, NullLogger.Instance, maxRetryCount: 1);
        var responses = client.EmptyS3Bucket("bucket").Result.ToList();

        Assert.That(responses, Has.Count.EqualTo(1));
        s3Mock.Verify(m => m.DeleteObjectsAsync(It.IsAny<DeleteObjectsRequest>(), default), Times.Once);
    }

    [Test]
    public void S3Client_PutObjectsInS3BucketSync_StoresItems_And_CreatesBucketWhenMissing()
    {
        var s3Mock = new Mock<IAmazonS3>();
        s3Mock.Setup(client => client.EnsureBucketExistsAsync("bucket"))
            .Returns(Task.CompletedTask);
        s3Mock.Setup(client => client.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        var client = new S3Client(s3Mock.Object, NullLogger.Instance, maxRetryCount: 1);
        var responses = client.PutObjectsInS3BucketSync("bucket",
            [
                new KeyValuePair<string, byte[]>("k1", Encoding.UTF8.GetBytes("v1")),
                new KeyValuePair<string, byte[]>("k2", Encoding.UTF8.GetBytes("v2"))
            ]).ToList();

        Assert.That(responses, Has.Count.EqualTo(2));
        s3Mock.Verify(mock => mock.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Exactly(2));

        var missingBucketMock = new Mock<IAmazonS3>();
        missingBucketMock.Setup(client => client.EnsureBucketExistsAsync("missing"))
            .ThrowsAsync(new AmazonS3Exception("missing") { StatusCode = HttpStatusCode.NotFound });
        missingBucketMock.Setup(client => client.PutBucketAsync("missing", default))
            .ReturnsAsync(new PutBucketResponse());
        missingBucketMock.Setup(client => client.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        var missingBucketClient = new S3Client(missingBucketMock.Object, NullLogger.Instance, maxRetryCount: 1);
        var fallbackResponses = missingBucketClient.PutObjectsInS3BucketSync("missing",
            [
                new KeyValuePair<string, byte[]>("k", Encoding.UTF8.GetBytes("v"))
            ]).ToList();

        Assert.That(fallbackResponses, Has.Count.EqualTo(1));
        missingBucketMock.Verify(mock => mock.PutBucketAsync("missing", default), Times.Once);
    }

    [Test]
    public void S3Protocol_ReadChunkAndSend_WorkWithInjectedClient()
    {
        var now = DateTime.UtcNow;
        var objects = new[]
        {
            new S3Object { Key = "a", LastModified = now.AddSeconds(-2), Size = 3 },
            new S3Object { Key = "b", LastModified = now.AddSeconds(-1), Size = 4 }
        };

        var amazonClientMock = new Mock<IAmazonS3>();
        amazonClientMock.Setup(client => client.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse());

        var fakeClient = new FakeS3Client
        {
            Client = amazonClientMock.Object,
            ListObjects = (_, _, _) => Task.FromResult<IEnumerable<S3Object>>(objects),
            GetAllObjects = (_, _, _, _) => objects.Select(obj =>
                new KeyValuePair<S3Object, byte[]?>(obj, Encoding.UTF8.GetBytes(obj.Key!)))
        };

        var readerProtocol = new S3Protocol(new S3BucketReaderConfig
        {
            StorageBucket = "bucket",
            ServiceURL = "http://127.0.0.1",
            AccessKey = "ak",
            SecretKey = "sk",
            ReadFromRunStartTime = false
        }, new DataFilter { Body = true }, Globals.Logger);
        SetPrivateField(readerProtocol, "_s3Client", fakeClient);

        var readerNoBodyProtocol = new S3Protocol(new S3BucketReaderConfig
        {
            StorageBucket = "bucket",
            ServiceURL = "http://127.0.0.1",
            AccessKey = "ak",
            SecretKey = "sk",
            ReadFromRunStartTime = false
        }, new DataFilter { Body = false }, Globals.Logger);
        SetPrivateField(readerNoBodyProtocol, "_s3Client", fakeClient);

        var senderProtocol = new S3Protocol(new S3BucketSenderConfig
        {
            StorageBucket = "bucket",
            ServiceURL = "http://127.0.0.1",
            AccessKey = "ak",
            SecretKey = "sk",
            Prefix = "pref-"
        }, Globals.Logger);
        SetPrivateField(senderProtocol, "_s3Client", fakeClient);

        var withBody = readerProtocol.ReadChunk(TimeSpan.Zero).ToList();
        var noBody = readerNoBodyProtocol.ReadChunk(TimeSpan.Zero).ToList();
        var sent = senderProtocol.Send(new Data<object>
        {
            Body = Encoding.UTF8.GetBytes("payload"),
            MetaData = new MetaData { Storage = new Storage { Key = "k1" } }
        });

        senderProtocol.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(withBody, Has.Count.EqualTo(2));
            Assert.That(withBody.All(item => item.Body is byte[]), Is.True);
            Assert.That(noBody, Has.Count.EqualTo(2));
            Assert.That(noBody.All(item => item.Body == null), Is.True);
            Assert.That(sent.Body, Is.TypeOf<byte[]>());
            amazonClientMock.Verify(client => client.PutObjectAsync(
                It.Is<PutObjectRequest>(request => request.BucketName == "bucket" && request.Key == "pref-k1"),
                default), Times.Once);
            Assert.That(fakeClient.Disposed, Is.True);
        });
    }

    [Test]
    public void S3Protocol_ReadChunk_PreservesBytesForMultipleDeserializers()
    {
        var now = DateTime.UtcNow;
        var jsonPayload = new SerializableS3Payload { Name = "json", Count = 1 };
        var yamlPayload = new SerializableS3Payload { Name = "yaml", Count = 2 };
        var binaryPayload = new SerializableS3Payload { Name = "binary", Count = 3 };
        var messagePackPayload = "message-pack";
        var xmlPayload = XDocument.Parse("<root><value>42</value></root>");
        var protobufPayload = new StringValue { Value = "protobuf" };

        var scenarios = new[]
        {
            new S3DeserializationScenario(
                "json",
                new QaaS.Framework.Serialization.Serializers.Json().Serialize(jsonPayload)!,
                bytes => new QaaS.Framework.Serialization.Deserializers.Json().Deserialize(bytes, typeof(SerializableS3Payload)),
                result =>
                {
                    var payload = result as SerializableS3Payload;
                    Assert.That(payload, Is.Not.Null);
                    Assert.That(payload!.Name, Is.EqualTo("json"));
                    Assert.That(payload.Count, Is.EqualTo(1));
                }),
            new S3DeserializationScenario(
                "yaml",
                new QaaS.Framework.Serialization.Serializers.Yaml().Serialize(yamlPayload)!,
                bytes => new QaaS.Framework.Serialization.Deserializers.Yaml().Deserialize(bytes, typeof(SerializableS3Payload)),
                result =>
                {
                    var payload = result as SerializableS3Payload;
                    Assert.That(payload, Is.Not.Null);
                    Assert.That(payload!.Name, Is.EqualTo("yaml"));
                    Assert.That(payload.Count, Is.EqualTo(2));
                }),
            new S3DeserializationScenario(
                "binary",
                new QaaS.Framework.Serialization.Serializers.Binary().Serialize(binaryPayload)!,
                bytes => new QaaS.Framework.Serialization.Deserializers.Binary()
                    .Deserialize(bytes, typeof(SerializableS3Payload)),
                result =>
                {
                    var payload = result as SerializableS3Payload;
                    Assert.That(payload, Is.Not.Null);
                    Assert.That(payload!.Name, Is.EqualTo("binary"));
                    Assert.That(payload.Count, Is.EqualTo(3));
                }),
            new S3DeserializationScenario(
                "message-pack",
                new QaaS.Framework.Serialization.Serializers.MessagePack().Serialize(messagePackPayload)!,
                bytes => new QaaS.Framework.Serialization.Deserializers.MessagePack().Deserialize(bytes, typeof(string)),
                result => Assert.That(result, Is.EqualTo(messagePackPayload))),
            new S3DeserializationScenario(
                "xml",
                new QaaS.Framework.Serialization.Serializers.Xml().Serialize(xmlPayload)!,
                bytes => new QaaS.Framework.Serialization.Deserializers.Xml().Deserialize(bytes),
                result =>
                {
                    var payload = result as XDocument;
                    Assert.That(payload, Is.Not.Null);
                    Assert.That(payload!.Root!.Element("value")!.Value, Is.EqualTo("42"));
                }),
            new S3DeserializationScenario(
                "protobuf",
                new QaaS.Framework.Serialization.Serializers.ProtobufMessage().Serialize(protobufPayload)!,
                bytes => new QaaS.Framework.Serialization.Deserializers.ProtobufMessage().Deserialize(bytes, typeof(StringValue)),
                result =>
                {
                    var payload = result as StringValue;
                    Assert.That(payload, Is.Not.Null);
                    Assert.That(payload!.Value, Is.EqualTo("protobuf"));
                })
        };

        var s3Objects = scenarios.Select((scenario, index) => new S3Object
        {
            Key = scenario.Key,
            LastModified = now.AddMilliseconds(index),
            Size = scenario.Payload.Length
        }).ToArray();

        var fakeClient = new FakeS3Client
        {
            Client = new Mock<IAmazonS3>().Object,
            GetAllObjects = (_, _, _, _) => s3Objects.Zip(scenarios,
                (s3Object, scenario) => new KeyValuePair<S3Object, byte[]?>(s3Object, scenario.Payload))
        };

        var protocol = new S3Protocol(new S3BucketReaderConfig
        {
            StorageBucket = "bucket",
            ServiceURL = "http://127.0.0.1",
            AccessKey = "ak",
            SecretKey = "sk",
            ReadFromRunStartTime = false
        }, new DataFilter { Body = true }, Globals.Logger);
        SetPrivateField(protocol, "_s3Client", fakeClient);

        var consumed = protocol.ReadChunk(TimeSpan.Zero).ToList();

        Assert.That(consumed, Has.Count.EqualTo(scenarios.Length));

        foreach (var scenario in scenarios)
        {
            var rawBytes = consumed.Single(item => item.MetaData?.Storage?.Key == scenario.Key).Body as byte[];
            Assert.That(rawBytes, Is.EqualTo(scenario.Payload), $"S3 should preserve raw bytes for {scenario.Key}");
            scenario.AssertResult(scenario.Deserialize(rawBytes));
        }
    }

    [Test]
    public void S3Protocol_ReadChunk_ReturnsNullBodyForEmptyObjects_WhenSkipEmptyObjectsDisabled()
    {
        var now = DateTime.UtcNow;
        var emptyObject = new S3Object { Key = "empty", LastModified = now, Size = 0 };
        var fakeClient = new FakeS3Client
        {
            Client = new Mock<IAmazonS3>().Object,
            GetAllObjects = (_, _, _, skipEmptyObjects) =>
            {
                Assert.That(skipEmptyObjects, Is.False);
                return
                [
                    new KeyValuePair<S3Object, byte[]?>(emptyObject, null)
                ];
            }
        };

        var protocol = new S3Protocol(new S3BucketReaderConfig
        {
            StorageBucket = "bucket",
            ServiceURL = "http://127.0.0.1",
            AccessKey = "ak",
            SecretKey = "sk",
            SkipEmptyObjects = false,
            ReadFromRunStartTime = false
        }, new DataFilter { Body = true }, Globals.Logger);
        SetPrivateField(protocol, "_s3Client", fakeClient);

        var consumed = protocol.ReadChunk(TimeSpan.Zero).Single();

        Assert.Multiple(() =>
        {
            Assert.That(consumed.MetaData?.Storage?.Key, Is.EqualTo("empty"));
            Assert.That(consumed.Body, Is.Null);
            Assert.That(new QaaS.Framework.Serialization.Deserializers.Json().Deserialize(consumed.Body as byte[],
                typeof(SerializableS3Payload)), Is.Null);
        });
    }

    [Test]
    public void S3Protocol_ReadChunk_FiltersObjectsFromRunStartTime_AndUsesGeneratedKeys()
    {
        var now = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        var objects = new[]
        {
            new S3Object { Key = "old", LastModified = now.AddSeconds(-2), Size = 3 },
            new S3Object { Key = "new", LastModified = now, Size = 3 }
        };
        var amazonClientMock = new Mock<IAmazonS3>();
        amazonClientMock.Setup(client => client.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse());
        var fakeClient = new FakeS3Client
        {
            Client = amazonClientMock.Object,
            ListObjects = (_, _, _) => Task.FromResult<IEnumerable<S3Object>>(objects),
            GetAllObjects = (_, _, _, _) => objects.Select(s3Object =>
                new KeyValuePair<S3Object, byte[]?>(s3Object, Encoding.UTF8.GetBytes(s3Object.Key!)))
        };

        var readerProtocol = new ControlledTimeS3Protocol(new S3BucketReaderConfig
        {
            StorageBucket = "bucket",
            ServiceURL = "http://127.0.0.1",
            AccessKey = "ak",
            SecretKey = "sk",
            ReadFromRunStartTime = true
        }, new DataFilter { Body = true }, now);
        SetPrivateField(readerProtocol, "_s3Client", fakeClient);

        var senderProtocol = new S3Protocol(new S3BucketSenderConfig
        {
            StorageBucket = "bucket",
            ServiceURL = "http://127.0.0.1",
            AccessKey = "ak",
            SecretKey = "sk",
            Prefix = "pref-",
            S3SentObjectsNaming = ObjectNamingGeneratorType.GrowingNumericalSeries
        }, Globals.Logger);
        SetPrivateField(senderProtocol, "_s3Client", fakeClient);

        var read = readerProtocol.ReadChunk(TimeSpan.Zero).ToList();
        var sent = senderProtocol.Send(new Data<object> { Body = Encoding.UTF8.GetBytes("payload"), MetaData = null });

        Assert.Multiple(() =>
        {
            Assert.That(read.Select(item => item.MetaData?.Storage?.Key), Is.EqualTo(new[] { "new" }));
            Assert.That(sent.Body, Is.TypeOf<byte[]>());
            amazonClientMock.Verify(client => client.PutObjectAsync(
                It.Is<PutObjectRequest>(request => request.Key == "pref-0"),
                default), Times.Once);
        });
    }

    [Test]
    public void S3Protocol_PrivateInactivityHelper_HandlesMissingObjectsAndUnspecifiedDateKinds()
    {
        var fakeClient = new FakeS3Client
        {
            Client = new Mock<IAmazonS3>().Object,
            ListObjects = (_, _, _) => Task.FromResult<IEnumerable<S3Object>>(
            [
                new S3Object
                {
                    Key = "unspecified",
                    LastModified = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Unspecified),
                    Size = 1
                }
            ])
        };
        var protocol = new ControlledTimeS3Protocol(new S3BucketReaderConfig
        {
            StorageBucket = "bucket",
            ServiceURL = "http://127.0.0.1",
            AccessKey = "ak",
            SecretKey = "sk",
            ReadFromRunStartTime = false
        }, new DataFilter { Body = false }, new DateTime(2026, 3, 15, 12, 0, 1, DateTimeKind.Utc));
        SetPrivateField(protocol, "_s3Client", fakeClient);

        var helper = typeof(S3Protocol).GetMethod("GetNumberOfMilliSecondsPassedSinceLastS3ObjectModification",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var unspecifiedResult = helper.Invoke(protocol, null);

        var emptyClient = new FakeS3Client
        {
            Client = new Mock<IAmazonS3>().Object,
            ListObjects = (_, _, _) => Task.FromResult<IEnumerable<S3Object>>([])
        };
        SetPrivateField(protocol, "_s3Client", emptyClient);
        var emptyResult = helper.Invoke(protocol, null);

        Assert.Multiple(() =>
        {
            Assert.That(unspecifiedResult, Is.Null);
            Assert.That(emptyResult, Is.Null);
        });
    }

    [Test]
    public void S3Protocol_ConnectAndDispose_CoverSenderReaderAndNullClientBranches()
    {
        var sender = new S3Protocol(new S3BucketSenderConfig
        {
            StorageBucket = "bucket",
            ServiceURL = "http://127.0.0.1:9000",
            AccessKey = "ak",
            SecretKey = "sk"
        }, Globals.Logger);
        var reader = new S3Protocol(new S3BucketReaderConfig
        {
            StorageBucket = "bucket",
            ServiceURL = "http://127.0.0.1:9000",
            AccessKey = "ak",
            SecretKey = "sk"
        }, new DataFilter(), Globals.Logger);
        var noClient = new S3Protocol(new S3BucketSenderConfig
        {
            StorageBucket = "bucket",
            ServiceURL = "http://127.0.0.1:9000",
            AccessKey = "ak",
            SecretKey = "sk"
        }, Globals.Logger);

        sender.Connect();
        reader.Connect();
        noClient.Dispose();
        sender.Dispose();
        reader.Dispose();

        Assert.Pass();
    }

    [Test]
    public void RedisProtocol_SendChunk_SucceedsAndFailsAccordingToTransactionResult()
    {
        var transactionMock = new Mock<ITransaction>();
        transactionMock.Setup(transaction => transaction.ExecuteAsync(CommandFlags.None)).ReturnsAsync(true);

        var databaseMock = new Mock<IDatabase>();
        databaseMock.Setup(database => database.CreateTransaction(It.IsAny<object?>())).Returns(transactionMock.Object);

        var config = new RedisSenderConfig
        {
            HostNames = ["localhost:6379"],
            RedisDataType = RedisDataType.SetString,
            Retries = 1,
            RetryIntervalMs = 1
        };

        var protocol = new QaaS.Framework.Protocols.Protocols.RedisProtocol(config, Globals.Logger);
        SetPrivateField(protocol, "_redisDb", databaseMock.Object);

        var result = protocol.SendChunk([
            new Data<object>
            {
                Body = Encoding.UTF8.GetBytes("value"),
                MetaData = new MetaData { Redis = new Redis { Key = "k" } }
            }
        ]).ToList();

        Assert.That(result, Has.Count.EqualTo(1));

        transactionMock.Reset();
        transactionMock.Setup(transaction => transaction.ExecuteAsync(CommandFlags.None)).ReturnsAsync(false);
        databaseMock.Setup(database => database.CreateTransaction(It.IsAny<object?>())).Returns(transactionMock.Object);

        Assert.Throws<RedisException>(() =>
            protocol.SendChunk([
                new Data<object>
                {
                    Body = Encoding.UTF8.GetBytes("value"),
                    MetaData = new MetaData { Redis = new Redis { Key = "k" } }
                }
            ]).ToList());
    }

    [Test]
    public void RedisProtocol_AddToTransaction_CoversAllRedisDataTypes()
    {
        var method = typeof(QaaS.Framework.Protocols.Protocols.RedisProtocol).GetMethod("AddToRedisTransactionByRedisType",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        foreach (var redisType in Enum.GetValues<RedisDataType>())
        {
            var protocol = new QaaS.Framework.Protocols.Protocols.RedisProtocol(new RedisSenderConfig
            {
                HostNames = ["localhost:6379"],
                RedisDataType = redisType
            }, Globals.Logger);

            var transaction = new Mock<ITransaction>().Object;
            var data = new Data<byte[]>
            {
                Body = Encoding.UTF8.GetBytes("v"),
                MetaData = new MetaData
                {
                    Redis = new Redis
                    {
                        Key = "k",
                        HashField = "h",
                        SetScore = 1.1,
                        GeoLatitude = 1,
                        GeoLongitude = 2
                    }
                }
            };

            Assert.DoesNotThrow(() => method.Invoke(protocol, [transaction, data]));
        }
    }

    [Test]
    public void RedisReaderProtocol_Read_ConsumesStringAndDeletesKey()
    {
        var databaseMock = new Mock<IDatabase>();
        databaseMock.Setup(database => database.StringGetDelete("k", CommandFlags.None))
            .Returns((RedisValue)Encoding.UTF8.GetBytes("value"));

        var protocol = new RedisReaderProtocol(new RedisReaderConfig
        {
            HostNames = ["localhost:6379"],
            Key = "k",
            RedisDataType = RedisDataType.SetString,
            PollIntervalMs = 1
        }, Globals.Logger);
        SetPrivateField(protocol, "_redisDb", databaseMock.Object);

        var result = protocol.Read(TimeSpan.FromMilliseconds(10));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.MetaData?.Redis?.Key, Is.EqualTo("k"));
        Assert.That(Encoding.UTF8.GetString((byte[])result.Body!), Is.EqualTo("value"));
        databaseMock.Verify(database => database.StringGetDelete("k", CommandFlags.None), Times.Once);
        databaseMock.Verify(database => database.KeyDelete("k", CommandFlags.None), Times.Never);
    }

    [Test]
    public void RedisReaderProtocol_Read_ConsumesHashAndDeletesField()
    {
        var databaseMock = new Mock<IDatabase>();
        databaseMock.Setup(database => database.HashGet("hash-key", "field", CommandFlags.None))
            .Returns((RedisValue)Encoding.UTF8.GetBytes("value"));
        databaseMock.Setup(database => database.HashDelete("hash-key", "field", CommandFlags.None))
            .Returns(true);

        var protocol = new RedisReaderProtocol(new RedisReaderConfig
        {
            HostNames = ["localhost:6379"],
            Key = "hash-key",
            HashField = "field",
            RedisDataType = RedisDataType.HashSet,
            PollIntervalMs = 1
        }, Globals.Logger);
        SetPrivateField(protocol, "_redisDb", databaseMock.Object);

        var result = protocol.Read(TimeSpan.FromMilliseconds(10));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.MetaData?.Redis?.HashField, Is.EqualTo("field"));
        Assert.That(Encoding.UTF8.GetString((byte[])result.Body!), Is.EqualTo("value"));
        databaseMock.Verify(database => database.HashDelete("hash-key", "field", CommandFlags.None), Times.Once);
    }

    [Test]
    public void RedisReaderProtocol_Read_WhenNoValueAvailable_ReturnsNullAfterTimeout()
    {
        var databaseMock = new Mock<IDatabase>();
        databaseMock.Setup(database => database.ListLeftPop("queue", CommandFlags.None))
            .Returns(RedisValue.Null);

        var protocol = new RedisReaderProtocol(new RedisReaderConfig
        {
            HostNames = ["localhost:6379"],
            Key = "queue",
            RedisDataType = RedisDataType.ListLeftPush,
            PollIntervalMs = 1
        }, Globals.Logger);
        SetPrivateField(protocol, "_redisDb", databaseMock.Object);

        var result = protocol.Read(TimeSpan.FromMilliseconds(5));

        Assert.That(result, Is.Null);
    }

    [Test]
    public void RedisReaderProtocol_Read_WhenGeoConfigured_ThrowsNotSupportedException()
    {
        var databaseMock = new Mock<IDatabase>();
        var protocol = new RedisReaderProtocol(new RedisReaderConfig
        {
            HostNames = ["localhost:6379"],
            Key = "geo",
            RedisDataType = RedisDataType.GeoAdd,
            PollIntervalMs = 1
        }, Globals.Logger);
        SetPrivateField(protocol, "_redisDb", databaseMock.Object);

        Assert.Throws<NotSupportedException>(() => protocol.Read(TimeSpan.FromMilliseconds(1)));
    }

    [Test]
    public void SocketProtocol_Read_UsesOverriddenMessageReader()
    {
        var protocol = new TestSocketProtocol(new SocketReaderConfig
        {
            Host = "127.0.0.1",
            Port = 1000,
            ProtocolType = ProtocolType.Tcp
        });
        protocol.Responses.Enqueue(Encoding.UTF8.GetBytes("hello"));

        var read = protocol.Read(TimeSpan.FromSeconds(1));

        Assert.That(read, Is.Not.Null);
        Assert.That(Encoding.UTF8.GetString((byte[])read!.Body!), Is.EqualTo("hello"));
        Assert.That(protocol.GetSerializationType(), Is.Null);
    }

    [Test]
    public void SocketProtocol_Read_WhenNoMessage_ReturnsNullOnTimeout()
    {
        var protocol = new TestSocketProtocol(new SocketReaderConfig
        {
            Host = "127.0.0.1",
            Port = 1000,
            ProtocolType = ProtocolType.Tcp
        });

        var read = protocol.Read(TimeSpan.FromMilliseconds(30));
        Assert.That(read, Is.Null);
    }

    [Test]
    public void IbmMqProtocol_Read_WithOverriddenMessage_ReturnsBytes()
    {
        var protocol = new TestIbmMqProtocol(new IbmMqReaderConfig
        {
            HostName = "h",
            Port = 1414,
            Channel = "c",
            Manager = "m",
            QueueName = "q"
        });
        var message = new MQMessage();
        message.WriteBytes("abc");
        protocol.NextMessage = message;

        var read = protocol.Read(TimeSpan.FromMilliseconds(10));

        Assert.That(read, Is.Not.Null);
        Assert.That(read!.Body, Is.TypeOf<byte[]>());
        Assert.That(protocol.GetSerializationType(), Is.Null);
        Assert.DoesNotThrow(() => protocol.Disconnect());
        Assert.DoesNotThrow(() => protocol.Dispose());
    }

    [Test]
    public void MongoDbProtocol_SendChunk_HandlesEmptyAndNonEmptyChunks()
    {
        var collectionMock = new Mock<IMongoCollection<BsonDocument>>();
        var protocol = new MongoDbProtocol(new MongoDbCollectionSenderConfig
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "db",
            CollectionName = "c"
        }, Globals.Logger);
        SetPrivateField(protocol, "_mongoCollection", collectionMock.Object);

        var emptyResult = protocol.SendChunk([]).ToList();
        var nonEmptyResult = protocol.SendChunk([
            new Data<object> { Body = new { Id = 1, Name = "n1" } },
            new Data<object> { Body = new { Id = 2, Name = "n2" } }
        ]).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(emptyResult, Is.Empty);
            Assert.That(nonEmptyResult, Has.Count.EqualTo(2));
            Assert.That(protocol.GetSerializationType(), Is.EqualTo(QaaS.Framework.Serialization.SerializationType.Json));
            collectionMock.Verify(collection => collection.InsertMany(
                It.IsAny<IEnumerable<BsonDocument>>(),
                null,
                default), Times.Once);
        });
    }

    [Test]
    public void MongoDbProtocol_Connect_And_SendChunk_WhenInsertFails_Throws()
    {
        var protocol = new MongoDbProtocol(new MongoDbCollectionSenderConfig
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "db",
            CollectionName = "c"
        }, Globals.Logger);

        Assert.DoesNotThrow(() => protocol.Connect());
        Assert.DoesNotThrow(() => protocol.Disconnect());

        var collectionMock = new Mock<IMongoCollection<BsonDocument>>();
        collectionMock.Setup(collection => collection.InsertMany(It.IsAny<IEnumerable<BsonDocument>>(), null, default))
            .Throws(new InvalidOperationException("insert failed"));
        SetPrivateField(protocol, "_mongoCollection", collectionMock.Object);

        Assert.Throws<InvalidOperationException>(() => protocol.SendChunk([
            new Data<object> { Body = new { Id = 1 } }
        ]).ToList());
    }

    [Test]
    public void SocketProtocol_Sender_Connects_Sends_And_Disconnects()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptTask = listener.AcceptTcpClientAsync();

        var protocol = new SocketProtocol(new SocketSenderConfig
        {
            Host = "127.0.0.1",
            Port = port,
            AddressFamily = AddressFamily.InterNetwork,
            SocketType = SocketType.Stream,
            ProtocolType = ProtocolType.Tcp,
            NagleAlgorithm = false
        }, Globals.Logger);

        protocol.Connect();
        var sent = protocol.Send(new Data<object> { Body = Encoding.UTF8.GetBytes("ping") });

        using var serverClient = acceptTask.GetAwaiter().GetResult();
        using var stream = serverClient.GetStream();
        var buffer = new byte[4];
        _ = stream.Read(buffer, 0, buffer.Length);

        protocol.Disconnect();
        protocol.Dispose();
        listener.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(protocol.GetSerializationType(), Is.Null);
            Assert.That(sent.Body, Is.TypeOf<byte[]>());
            Assert.That(Encoding.UTF8.GetString(buffer), Is.EqualTo("ping"));
        });
    }

    [Test]
    public void SqlDerivedProtocols_BuildExpectedQueries()
    {
        var now = DateTime.UtcNow;

        var postgres = new PostgreSqlProtocolWrapper(new PostgreSqlReaderConfig
        {
            ConnectionString = "Host=localhost;Username=u;Password=p;Database=db",
            TableName = "tbl",
            InsertionTimeField = "created_at",
            IsInsertionTimeFieldTimeZoneTz = true
        });
        postgres.ConfigureState("created_at", now, true, "id > 1");

        var mssql = new MsSqlProtocolWrapper(new MsSqlReaderConfig
        {
            ConnectionString = "Server=localhost;Database=db;User Id=u;Password=p;",
            TableName = "tbl",
            InsertionTimeField = "created_at"
        });
        mssql.ConfigureState("created_at", now, true, "id > 1");

        var oracle = new OracleSqlProtocolWrapper(new OracleReaderConfig
        {
            ConnectionString = "Data Source=localhost;User Id=u;Password=p;",
            TableName = "tbl",
            InsertionTimeField = "created_at"
        });
        oracle.ConfigureState("created_at", now, true, "id > 1");

        var trino = new TrinoSqlProtocolWrapper(new TrinoReaderConfig
        {
            ConnectionString = string.Empty,
            TableName = "tbl",
            InsertionTimeField = "created_at",
            Username = "u",
            Password = "p",
            ClientTag = "tag",
            Schema = "sch",
            Catalog = "cat",
            Hostname = "http://localhost:8080"
        });
        trino.ConfigureState("created_at", now, true, "id > 1");

        var postgresNoWhere = new PostgreSqlProtocolWrapper(new PostgreSqlReaderConfig
        {
            ConnectionString = "Host=localhost;Username=u;Password=p;Database=db",
            TableName = "tbl",
            InsertionTimeField = "created_at",
            IsInsertionTimeFieldTimeZoneTz = false
        });
        postgresNoWhere.ConfigureState("created_at", now, true, null);

        var mssqlNoFilter = new MsSqlProtocolWrapper(new MsSqlReaderConfig
        {
            ConnectionString = "Server=localhost;Database=db;User Id=u;Password=p;",
            TableName = "tbl",
            InsertionTimeField = "created_at"
        });
        mssqlNoFilter.ConfigureState("created_at", now, false, null);

        var oracleNoFilter = new OracleSqlProtocolWrapper(new OracleReaderConfig
        {
            ConnectionString = "Data Source=localhost;User Id=u;Password=p;",
            TableName = "tbl",
            InsertionTimeField = "created_at"
        });
        oracleNoFilter.ConfigureState("created_at", now, false, null);

        Assert.Multiple(() =>
        {
            Assert.That(postgres.AscQuery(), Does.Contain("order by \"created_at\" asc"));
            Assert.That(postgres.PlainQuery(), Does.Contain("where"));
            Assert.That(postgres.LatestQuery(), Does.Contain("desc LIMIT 1"));
            Assert.That(postgres.Format(now), Does.Contain("TO_TIMESTAMP"));
            Assert.That(postgresNoWhere.PlainQuery(), Does.Not.Contain(" and "));
            Assert.That(postgresNoWhere.AscQuery(), Does.Contain("\"created_at\""));

            Assert.That(mssql.AscQuery(), Does.Contain("order by created_at asc"));
            Assert.That(mssql.LatestQuery(), Does.Contain("top 1"));
            Assert.That(mssql.Format(now), Does.Contain(now.ToString("yyyy-MM-dd")));
            Assert.That(mssqlNoFilter.PlainQuery(), Does.Not.Contain("where"));

            Assert.That(oracle.AscQuery(), Does.Contain("order by created_at asc"));
            Assert.That(oracle.LatestQuery(), Does.Contain("ROWNUM <= 1"));
            Assert.That(oracle.Format(now), Does.Contain("TO_DATE"));
            Assert.That(oracleNoFilter.PlainQuery(), Does.Not.Contain("where"));

            Assert.That(trino.AscQuery(), Does.Contain("select * from sch.tbl"));
            Assert.That(trino.LatestQuery(), Does.Contain("limit 1"));
            Assert.That(trino.Format(now), Does.Contain("FROM_ISO8601_TIMESTAMP"));
        });
    }

    [Test]
    public void PostgreSqlProtocol_RequestsUserDefinedResultColumnsAsText()
    {
        var schemaReaderMock = new Mock<IDataReader>();
        schemaReaderMock.SetupGet(reader => reader.FieldCount).Returns(3);
        schemaReaderMock.Setup(reader => reader.GetName(0)).Returns("id");
        schemaReaderMock.Setup(reader => reader.GetName(1)).Returns("shape");
        schemaReaderMock.Setup(reader => reader.GetName(2)).Returns("custom_value");
        schemaReaderMock.Setup(reader => reader.GetDataTypeName(0)).Returns("integer");
        schemaReaderMock.Setup(reader => reader.GetDataTypeName(1)).Returns("public.geometry");
        schemaReaderMock.Setup(reader => reader.GetDataTypeName(2)).Returns("custom.my_type");

        var resultReaderMock = new Mock<IDataReader>();
        var protocol = new InspectablePostgreSqlProtocol(new PostgreSqlReaderConfig
        {
            ConnectionString = "Host=localhost;Username=u;Password=p;Database=db",
            TableName = "tbl"
        }, schemaReaderMock.Object, resultReaderMock.Object);

        using var firstReader = protocol.InvokeExecuteReader(new NpgsqlCommand("select * from tbl"));
        using var secondReader = protocol.InvokeExecuteReader(new NpgsqlCommand("select * from tbl"));

        Assert.Multiple(() =>
        {
            Assert.That(protocol.LastUnknownResultTypeList, Is.EqualTo(new[] { false, true, true }));
            Assert.That(protocol.SchemaReaderCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void PostgreSqlProtocol_DoesNotDowngradeBuiltInResultColumnsToText()
    {
        var schemaReaderMock = new Mock<IDataReader>();
        schemaReaderMock.SetupGet(reader => reader.FieldCount).Returns(2);
        schemaReaderMock.Setup(reader => reader.GetDataTypeName(0)).Returns("integer");
        schemaReaderMock.Setup(reader => reader.GetDataTypeName(1)).Returns("timestamp with time zone");

        var resultReaderMock = new Mock<IDataReader>();
        var protocol = new InspectablePostgreSqlProtocol(new PostgreSqlReaderConfig
        {
            ConnectionString = "Host=localhost;Username=u;Password=p;Database=db",
            TableName = "tbl"
        }, schemaReaderMock.Object, resultReaderMock.Object);

        using var firstReader = protocol.InvokeExecuteReader(new NpgsqlCommand("select id, created_at from tbl"));
        using var secondReader = protocol.InvokeExecuteReader(new NpgsqlCommand("select id, created_at from tbl"));

        Assert.Multiple(() =>
        {
            Assert.That(protocol.LastUnknownResultTypeList, Is.Null);
            Assert.That(protocol.SchemaReaderCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void PostgreSqlProtocol_RequestsQualifiedNonPgCatalogTypesAsText()
    {
        var schemaReaderMock = new Mock<IDataReader>();
        schemaReaderMock.SetupGet(reader => reader.FieldCount).Returns(2);
        schemaReaderMock.Setup(reader => reader.GetName(0)).Returns("shape");
        schemaReaderMock.Setup(reader => reader.GetName(1)).Returns("custom_value");
        schemaReaderMock.Setup(reader => reader.GetDataTypeName(0)).Returns("postgis.geometry");
        schemaReaderMock.Setup(reader => reader.GetDataTypeName(1)).Returns("custom.my_type");

        var resultReaderMock = new Mock<IDataReader>();
        var protocol = new InspectablePostgreSqlProtocol(new PostgreSqlReaderConfig
        {
            ConnectionString = "Host=localhost;Username=u;Password=p;Database=db",
            TableName = "tbl"
        }, schemaReaderMock.Object, resultReaderMock.Object);

        using var firstReader = protocol.InvokeExecuteReader(new NpgsqlCommand("select shape, custom_value from tbl"));
        using var secondReader = protocol.InvokeExecuteReader(new NpgsqlCommand("select shape, custom_value from tbl"));

        Assert.Multiple(() =>
        {
            Assert.That(protocol.LastUnknownResultTypeList, Is.EqualTo(new[] { true, true }));
            Assert.That(protocol.SchemaReaderCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void PostgreSqlProtocol_DoesNotDowngradeQualifiedPgCatalogTypesToText()
    {
        var schemaReaderMock = new Mock<IDataReader>();
        schemaReaderMock.SetupGet(reader => reader.FieldCount).Returns(2);
        schemaReaderMock.Setup(reader => reader.GetDataTypeName(0)).Returns("pg_catalog.int4");
        schemaReaderMock.Setup(reader => reader.GetDataTypeName(1)).Returns("pg_catalog.timestamptz");

        var resultReaderMock = new Mock<IDataReader>();
        var protocol = new InspectablePostgreSqlProtocol(new PostgreSqlReaderConfig
        {
            ConnectionString = "Host=localhost;Username=u;Password=p;Database=db",
            TableName = "tbl"
        }, schemaReaderMock.Object, resultReaderMock.Object);

        using var firstReader = protocol.InvokeExecuteReader(new NpgsqlCommand("select id, created_at from tbl"));
        using var secondReader = protocol.InvokeExecuteReader(new NpgsqlCommand("select id, created_at from tbl"));

        Assert.Multiple(() =>
        {
            Assert.That(protocol.LastUnknownResultTypeList, Is.Null);
            Assert.That(protocol.SchemaReaderCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void PostgreSqlProtocol_RetriesSchemaInspectionAfterTransientFailure()
    {
        var schemaReaderMock = new Mock<IDataReader>();
        schemaReaderMock.SetupGet(reader => reader.FieldCount).Returns(2);
        schemaReaderMock.Setup(reader => reader.GetName(0)).Returns("id");
        schemaReaderMock.Setup(reader => reader.GetName(1)).Returns("shape");
        schemaReaderMock.Setup(reader => reader.GetDataTypeName(0)).Returns("integer");
        schemaReaderMock.Setup(reader => reader.GetDataTypeName(1)).Returns("public.geometry");

        var resultReaderMock = new Mock<IDataReader>();
        var protocol = new InspectablePostgreSqlProtocol(new PostgreSqlReaderConfig
        {
            ConnectionString = "Host=localhost;Username=u;Password=p;Database=db",
            TableName = "tbl"
        }, callNumber => callNumber == 1
            ? throw new InvalidOperationException("Transient schema inspection failure")
            : schemaReaderMock.Object, resultReaderMock.Object);

        using var firstReader = protocol.InvokeExecuteReader(new NpgsqlCommand("select id, shape from tbl"));
        Assert.That(protocol.LastUnknownResultTypeList, Is.Null);

        using var secondReader = protocol.InvokeExecuteReader(new NpgsqlCommand("select id, shape from tbl"));
        using var thirdReader = protocol.InvokeExecuteReader(new NpgsqlCommand("select id, shape from tbl"));

        Assert.Multiple(() =>
        {
            Assert.That(protocol.LastUnknownResultTypeList, Is.EqualTo(new[] { false, true }));
            Assert.That(protocol.SchemaReaderCalls, Is.EqualTo(2));
        });
    }

    private static void SetPrivateField<TValue>(object instance, string fieldName, TValue value)
    {
        var currentType = instance.GetType();
        while (currentType != null)
        {
            var fieldInfo = currentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfo != null)
            {
                fieldInfo.SetValue(instance, value);
                return;
            }

            currentType = currentType.BaseType;
        }

        throw new MissingFieldException(instance.GetType().FullName, fieldName);
    }
}
