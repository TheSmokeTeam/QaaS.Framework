using System.Collections.Immutable;
using System.Data;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using IBM.WMQ;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class ProtocolAdvancedBehaviorTests
{
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
                ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes("abc"))
            });

        var client = new S3Client(s3Mock.Object, NullLogger.Instance, maxRetryCount: 1);

        var nonEmpty = client.ListAllObjectsInS3Bucket("bucket", skipEmptyObjects: true).Result.ToList();
        var withEmpty = client.ListAllObjectsInS3Bucket("bucket", skipEmptyObjects: false).Result.ToList();
        var fromMetadata = client.GetObjectFromObjectMetadata(new S3Object { Key = "meta" }, "bucket");

        Assert.Multiple(() =>
        {
            Assert.That(nonEmpty.Select(obj => obj.Key), Is.EqualTo(["full"]));
            Assert.That(withEmpty, Has.Count.EqualTo(2));
            Assert.That(Encoding.UTF8.GetString(fromMetadata.Value!), Is.EqualTo("abc"));
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
            amazonClientMock.Verify(client => client.Dispose(), Times.Once);
            Assert.That(fakeClient.Disposed, Is.True);
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(postgres.AscQuery(), Does.Contain("order by \"created_at\" asc"));
            Assert.That(postgres.PlainQuery(), Does.Contain("where"));
            Assert.That(postgres.LatestQuery(), Does.Contain("desc LIMIT 1"));
            Assert.That(postgres.Format(now), Does.Contain("TO_TIMESTAMP"));

            Assert.That(mssql.AscQuery(), Does.Contain("order by created_at asc"));
            Assert.That(mssql.LatestQuery(), Does.Contain("top 1"));
            Assert.That(mssql.Format(now), Does.Contain(now.ToString("yyyy-MM-dd")));

            Assert.That(oracle.AscQuery(), Does.Contain("order by created_at asc"));
            Assert.That(oracle.LatestQuery(), Does.Contain("ROWNUM <= 1"));
            Assert.That(oracle.Format(now), Does.Contain("TO_DATE"));

            Assert.That(trino.AscQuery(), Does.Contain("select * from sch.tbl"));
            Assert.That(trino.LatestQuery(), Does.Contain("limit 1"));
            Assert.That(trino.Format(now), Does.Contain("FROM_ISO8601_TIMESTAMP"));
        });
    }

    private static void SetPrivateField<TValue>(object instance, string fieldName, TValue value)
    {
        var fieldInfo = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        fieldInfo!.SetValue(instance, value);
    }
}
