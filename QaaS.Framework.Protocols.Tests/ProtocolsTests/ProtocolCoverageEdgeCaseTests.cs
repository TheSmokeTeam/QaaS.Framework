using System.Data;
using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QaaS.Framework.Infrastructure;
using QaaS.Framework.Protocols.ConfigurationObjects.RabbitMq;
using QaaS.Framework.Protocols.ConfigurationObjects.Redis;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class ProtocolCoverageEdgeCaseTests
{
    private sealed class FormattingSqlProtocol(SqlConfig config, IDbConnection dbConnection)
        : MockSqlProtocol("tbl", config, Globals.Logger, dbConnection)
    {
        protected override string GetTimeFieldSqlFormat(DateTime time) => $"TO_DATE('{time:yyyy-MM-dd HH:mm:ss}')";
    }

    private static void SetPrivateField<TValue>(object instance, string fieldName, TValue value)
    {
        instance.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(instance, value);
    }

    [Test]
    public void RowInsertIntoTable_UsesSqlSpecificDateFormatting()
    {
        string? commandText = null;
        var dbCommandMock = new Mock<IDbCommand>();
        dbCommandMock.SetupSet(command => command.CommandText = It.IsAny<string>())
            .Callback<string>(value => commandText = value);
        dbCommandMock.Setup(command => command.ExecuteNonQuery()).Returns(1);

        var connectionMock = new Mock<IDbConnection>();
        connectionMock.Setup(connection => connection.CreateCommand()).Returns(dbCommandMock.Object);

        var protocol = new FormattingSqlProtocol(new SqlConfig
        {
            ConnectionString = "Host=localhost",
            TableName = "tbl"
        }, connectionMock.Object);

        var dataTable = new DataTable();
        dataTable.Columns.Add("created_at", typeof(DateTime));
        dataTable.Rows.Add(new DateTime(2026, 1, 2, 3, 4, 5));

        typeof(BaseSqlProtocol<IDbConnection>)
            .GetMethod("RowInsertIntoTable", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(protocol, [dataTable]);

        Assert.That(commandText, Does.Contain("TO_DATE('2026-01-02 03:04:05')"));
    }

    [Test]
    public void DateTimeAndFileSystemExtensions_HandleWinterSummerCustomZonesAndInvalidCharacters()
    {
        var localTime = new DateTime(2026, 1, 1, 12, 0, 0);
        var winterUtc = localTime.ConvertDateTimeToUtcByTimeZoneOffset(3, false);
        var summerUtc = localTime.ConvertDateTimeToUtcByTimeZoneOffset(3, true);
        var winterLocal = winterUtc.ConvertDateTimeFromUtcToTimeZoneByTimeZoneOffset(3, false);
        var londonWinterUtc = localTime.ConvertDateTimeToUtcByTimeZoneOffset(1, timeZoneId: "Europe/London");
        var londonSummerLocal = new DateTime(2026, 7, 1, 11, 0, 0, DateTimeKind.Utc)
            .ConvertDateTimeFromUtcToTimeZoneByTimeZoneOffset(1, timeZoneId: "Europe/London");
        var invalidCharacter = Path.GetInvalidFileNameChars().First();
        var sanitized = FileSystemExtensions.MakeValidDirectoryName($"bad{invalidCharacter}name");

        Assert.Multiple(() =>
        {
            Assert.That(winterUtc, Is.EqualTo(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc)));
            Assert.That(summerUtc, Is.EqualTo(new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)));
            Assert.That(winterLocal, Is.EqualTo(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Unspecified)));
            Assert.That(londonWinterUtc, Is.EqualTo(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
            Assert.That(londonSummerLocal, Is.EqualTo(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Unspecified)));
            Assert.That(sanitized, Is.EqualTo("bad_name"));
            Assert.That(FileSystemExtensions.MakeValidDirectoryName(null), Is.Null);
        });
    }

    [Test]
    public void RedisReaderProtocol_Read_ConsumesRightListSetAndSortedSetValues()
    {
        var listDatabase = new Mock<IDatabase>();
        listDatabase.Setup(database => database.ListRightPop("right", CommandFlags.None))
            .Returns((RedisValue)Encoding.UTF8.GetBytes("right-value"));
        var listProtocol = new RedisReaderProtocol(new RedisReaderConfig
        {
            HostNames = ["localhost:6379"],
            Key = "right",
            RedisDataType = RedisDataType.ListRightPush,
            PollIntervalMs = 1
        }, Globals.Logger);
        SetPrivateField(listProtocol, "_redisDb", listDatabase.Object);

        var setDatabase = new Mock<IDatabase>();
        setDatabase.Setup(database => database.SetPop("set", CommandFlags.None))
            .Returns((RedisValue)Encoding.UTF8.GetBytes("set-value"));
        var setProtocol = new RedisReaderProtocol(new RedisReaderConfig
        {
            HostNames = ["localhost:6379"],
            Key = "set",
            RedisDataType = RedisDataType.SetAdd,
            PollIntervalMs = 1
        }, Globals.Logger);
        SetPrivateField(setProtocol, "_redisDb", setDatabase.Object);

        var sortedDatabase = new Mock<IDatabase>();
        sortedDatabase.Setup(database => database.SortedSetRangeByRankWithScores(
                "sorted",
                0,
                0,
                Order.Ascending,
                CommandFlags.None))
            .Returns([
                new SortedSetEntry((RedisValue)Encoding.UTF8.GetBytes("sorted-value"), 1.5)
            ]);
        sortedDatabase.Setup(database =>
                database.SortedSetRemove("sorted", It.IsAny<RedisValue>(), CommandFlags.None))
            .Returns(true);
        var sortedProtocol = new RedisReaderProtocol(new RedisReaderConfig
        {
            HostNames = ["localhost:6379"],
            Key = "sorted",
            RedisDataType = RedisDataType.SortedSetAdd,
            PollIntervalMs = 1
        }, Globals.Logger);
        SetPrivateField(sortedProtocol, "_redisDb", sortedDatabase.Object);

        var rightResult = listProtocol.Read(TimeSpan.FromMilliseconds(10));
        var setResult = setProtocol.Read(TimeSpan.FromMilliseconds(10));
        var sortedResult = sortedProtocol.Read(TimeSpan.FromMilliseconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(Encoding.UTF8.GetString((byte[])rightResult!.Body!), Is.EqualTo("right-value"));
            Assert.That(Encoding.UTF8.GetString((byte[])setResult!.Body!), Is.EqualTo("set-value"));
            Assert.That(Encoding.UTF8.GetString((byte[])sortedResult!.Body!), Is.EqualTo("sorted-value"));
            Assert.That(sortedResult.MetaData?.Redis?.SetScore, Is.EqualTo(1.5));
        });
    }

    [Test]
    public void RedisReaderProtocol_Read_ConsumesLeftListValue()
    {
        var databaseMock = new Mock<IDatabase>();
        databaseMock.Setup(database => database.ListLeftPop("left", CommandFlags.None))
            .Returns((RedisValue)Encoding.UTF8.GetBytes("left-value"));

        var protocol = new RedisReaderProtocol(new RedisReaderConfig
        {
            HostNames = ["localhost:6379"],
            Key = "left",
            RedisDataType = RedisDataType.ListLeftPush,
            PollIntervalMs = 1
        }, Globals.Logger);
        SetPrivateField(protocol, "_redisDb", databaseMock.Object);

        var result = protocol.Read(TimeSpan.FromMilliseconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(Encoding.UTF8.GetString((byte[])result!.Body!), Is.EqualTo("left-value"));
            Assert.That(result.MetaData?.Redis?.Key, Is.EqualTo("left"));
        });
    }

    [Test]
    public void RedisReaderProtocol_Read_ThrowsForMissingHashFieldAndMissingRedisType()
    {
        var databaseMock = new Mock<IDatabase>();

        var missingHashFieldProtocol = new RedisReaderProtocol(new RedisReaderConfig
        {
            HostNames = ["localhost:6379"],
            Key = "hash",
            RedisDataType = RedisDataType.HashSet,
            PollIntervalMs = 1
        }, Globals.Logger);
        SetPrivateField(missingHashFieldProtocol, "_redisDb", databaseMock.Object);

        var missingTypeProtocol = new RedisReaderProtocol(new RedisReaderConfig
        {
            HostNames = ["localhost:6379"],
            Key = "none",
            RedisDataType = null,
            PollIntervalMs = 1
        }, Globals.Logger);
        SetPrivateField(missingTypeProtocol, "_redisDb", databaseMock.Object);

        Assert.Throws<ArgumentException>(() => missingHashFieldProtocol.Read(TimeSpan.FromMilliseconds(1)));
        Assert.Throws<InvalidOperationException>(() => missingTypeProtocol.Read(TimeSpan.FromMilliseconds(1)));
    }

    [Test]
    public void RabbitMqProtocol_Read_ReturnsMessageAfterPollingQueue()
    {
        var channelMock = new Mock<IChannel>();
        channelMock.Setup(mock => mock.QueueDeclarePassiveAsync("queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("queue", 0, 0));
        channelMock.SetupSequence(mock => mock.BasicGetAsync("queue", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BasicGetResult?)null)
            .ReturnsAsync(new BasicGetResult(
                deliveryTag: 1,
                redelivered: false,
                exchange: "exchange",
                routingKey: "queue",
                messageCount: 0,
                basicProperties: new BasicProperties
                {
                    ContentType = "application/octet-stream",
                    Headers = new Dictionary<string, object?> { ["h"] = "v" }
                },
                body: Encoding.UTF8.GetBytes("rabbit")));

        var protocol = new RabbitMqProtocol(new RabbitMqReaderConfig
        {
            Host = "localhost",
            QueueName = "queue",
            ExchangeName = "exchange",
            RoutingKey = "queue"
        }, NullLogger.Instance);
        SetPrivateField(protocol, "_channel", channelMock.Object);

        var result = protocol.Read(TimeSpan.FromMilliseconds(50));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(Encoding.UTF8.GetString((byte[])result!.Body!), Is.EqualTo("rabbit"));
            Assert.That(result.MetaData?.RabbitMq?.RoutingKey, Is.EqualTo("queue"));
            Assert.That(result.MetaData?.RabbitMq?.ContentType, Is.EqualTo("application/octet-stream"));
        });
    }

    [Test]
    public void RabbitMqProtocol_UsesSenderDefaultsAndReaderDefaultQueueBranches()
    {
        var queueSender = new RabbitMqProtocol(new RabbitMqSenderConfig
        {
            Host = "localhost",
            QueueName = "queue-name"
        }, NullLogger.Instance);
        var exchangeSender = new RabbitMqProtocol(new RabbitMqSenderConfig
        {
            Host = "localhost",
            ExchangeName = "exchange-name",
            RoutingKey = "routing-key",
            Headers = new Dictionary<string, object?> { ["h"] = "v" },
            ContentType = "application/octet-stream",
            Type = "kind",
            Expiration = "1000"
        }, NullLogger.Instance);
        var senderChannelMock = new Mock<IChannel>();
        senderChannelMock
            .Setup(mock => mock.ExchangeDeclarePassiveAsync("exchange-name", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        senderChannelMock
            .Setup(mock => mock.BasicPublishAsync("exchange-name", "routing-key", true,
                It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        SetPrivateField(exchangeSender, "_channel", senderChannelMock.Object);

        var queueReadChannelMock = new Mock<IChannel>();
        queueReadChannelMock.Setup(mock => mock.QueueDeclarePassiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("generated", 0, 0));
        queueReadChannelMock.Setup(mock => mock.BasicGetAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BasicGetResult?)null);
        var reader = new RabbitMqProtocol(new RabbitMqReaderConfig
        {
            Host = "localhost",
            QueueName = null,
            ExchangeName = "exchange",
            RoutingKey = "route"
        }, NullLogger.Instance);
        SetPrivateField(reader, "_channel", queueReadChannelMock.Object);

        var queueRoutingKey = (string)typeof(RabbitMqProtocol)
            .GetProperty("RoutingKey", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(queueSender)!;
        var queueExchangeName = (string)typeof(RabbitMqProtocol)
            .GetProperty("ExchangeName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(queueSender)!;
        var readerDefaultQueueName = (string)typeof(RabbitMqProtocol)
            .GetField("_defaultQueueName", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(reader)!;

        var sent = exchangeSender.Send(new Data<object> { Body = Encoding.UTF8.GetBytes("body"), MetaData = null });
        var read = reader.Read(TimeSpan.FromMilliseconds(5));

        Assert.Multiple(() =>
        {
            Assert.That(queueRoutingKey, Is.EqualTo("queue-name"));
            Assert.That(queueExchangeName, Is.EqualTo(string.Empty));
            Assert.That(readerDefaultQueueName, Does.StartWith("QaaS_"));
            Assert.That(sent.Body, Is.TypeOf<byte[]>());
            Assert.That(read, Is.Null);
        });
    }

    [Test]
    public void RabbitMqProtocol_Send_UsesMetadataOverridesWhenProvided()
    {
        BasicProperties? publishedProperties = null;
        string? publishedRoutingKey = null;
        var channelMock = new Mock<IChannel>();
        channelMock
            .Setup(mock => mock.ExchangeDeclarePassiveAsync("exchange-name", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        channelMock
            .Setup(mock => mock.BasicPublishAsync("exchange-name", It.IsAny<string>(), true,
                It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>(
                (_, routingKey, _, properties, _, _) =>
                {
                    publishedRoutingKey = routingKey;
                    publishedProperties = properties;
                })
            .Returns(ValueTask.CompletedTask);

        var sender = new RabbitMqProtocol(new RabbitMqSenderConfig
        {
            Host = "localhost",
            ExchangeName = "exchange-name",
            RoutingKey = "default-route",
            Headers = new Dictionary<string, object?> { ["default"] = "value" },
            Expiration = "1000",
            ContentType = "application/json",
            Type = "default-type"
        }, NullLogger.Instance);
        SetPrivateField(sender, "_channel", channelMock.Object);

        var sent = sender.Send(new Data<object>
        {
            Body = Encoding.UTF8.GetBytes("payload"),
            MetaData = new MetaData
            {
                RabbitMq = new RabbitMq
                {
                    RoutingKey = "override-route",
                    Headers = new Dictionary<string, object?> { ["override"] = "value" },
                    Expiration = "2000",
                    ContentType = "application/octet-stream",
                    Type = "override-type"
                }
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(sent.Body, Is.TypeOf<byte[]>());
            Assert.That(publishedRoutingKey, Is.EqualTo("override-route"));
            Assert.That(publishedProperties, Is.Not.Null);
            Assert.That(publishedProperties!.Headers, Contains.Key("override"));
            Assert.That(publishedProperties.Expiration, Is.EqualTo("2000"));
            Assert.That(publishedProperties.ContentType, Is.EqualTo("application/octet-stream"));
            Assert.That(publishedProperties.Type, Is.EqualTo("override-type"));
        });
    }

    [Test]
    public void RabbitMqProtocol_Send_OmitsUnsetMetadataProperties_And_UsesDefaultRoutingKey()
    {
        BasicProperties? publishedProperties = null;
        string? publishedRoutingKey = null;
        var channelMock = new Mock<IChannel>();
        channelMock
            .Setup(mock => mock.ExchangeDeclarePassiveAsync("exchange-name", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        channelMock
            .Setup(mock => mock.BasicPublishAsync<BasicProperties>("exchange-name", It.IsAny<string>(), true,
                It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>(
                (_, routingKey, _, properties, _, _) =>
                {
                    publishedRoutingKey = routingKey;
                    publishedProperties = properties;
                })
            .Returns(ValueTask.CompletedTask);

        var sender = new RabbitMqProtocol(new RabbitMqSenderConfig
        {
            Host = "localhost",
            ExchangeName = "exchange-name"
        }, NullLogger.Instance);
        SetPrivateField(sender, "_channel", channelMock.Object);

        var sent = sender.Send(new Data<object> { Body = Encoding.UTF8.GetBytes("payload"), MetaData = null });

        Assert.Multiple(() =>
        {
            Assert.That(sent.Body, Is.TypeOf<byte[]>());
            Assert.That(publishedRoutingKey, Is.EqualTo("/"));
            Assert.That(publishedProperties, Is.Null);
        });
    }

    [Test]
    public void RabbitMqProtocol_Send_OmitsWhitespaceAndEmptyMetadataProperties()
    {
        BasicProperties? publishedProperties = null;
        var channelMock = new Mock<IChannel>();
        channelMock
            .Setup(mock => mock.ExchangeDeclarePassiveAsync("exchange-name", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        channelMock
            .Setup(mock => mock.BasicPublishAsync<BasicProperties>("exchange-name", It.IsAny<string>(), true,
                It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>(
                (_, _, _, properties, _, _) => publishedProperties = properties)
            .Returns(ValueTask.CompletedTask);

        var sender = new RabbitMqProtocol(new RabbitMqSenderConfig
        {
            Host = "localhost",
            ExchangeName = "exchange-name",
            Headers = [],
            Expiration = "",
            ContentType = "   ",
            Type = string.Empty
        }, NullLogger.Instance);
        SetPrivateField(sender, "_channel", channelMock.Object);

        sender.Send(new Data<object> { Body = Encoding.UTF8.GetBytes("payload"), MetaData = null });

        Assert.That(publishedProperties, Is.Null);
    }

    [Test]
    public void RabbitMqProtocol_Send_UsesConfiguredDefaultMetadata_WhenMessageMetadataIsNull()
    {
        BasicProperties? publishedProperties = null;
        string? publishedRoutingKey = null;
        var channelMock = new Mock<IChannel>();
        channelMock
            .Setup(mock => mock.ExchangeDeclarePassiveAsync("exchange-name", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        channelMock
            .Setup(mock => mock.BasicPublishAsync("exchange-name", It.IsAny<string>(), true,
                It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>(
                (_, routingKey, _, properties, _, _) =>
                {
                    publishedRoutingKey = routingKey;
                    publishedProperties = properties;
                })
            .Returns(ValueTask.CompletedTask);

        var sender = new RabbitMqProtocol(new RabbitMqSenderConfig
        {
            Host = "localhost",
            ExchangeName = "exchange-name",
            RoutingKey = "default-route",
            Headers = new Dictionary<string, object?> { ["default"] = "value" },
            Expiration = "1500",
            ContentType = "application/json",
            Type = "default-type"
        }, NullLogger.Instance);
        SetPrivateField(sender, "_channel", channelMock.Object);

        sender.Send(new Data<object> { Body = Encoding.UTF8.GetBytes("payload"), MetaData = null });

        Assert.Multiple(() =>
        {
            Assert.That(publishedRoutingKey, Is.EqualTo("default-route"));
            Assert.That(publishedProperties, Is.Not.Null);
            Assert.That(publishedProperties!.Headers, Contains.Key("default"));
            Assert.That(publishedProperties.Expiration, Is.EqualTo("1500"));
            Assert.That(publishedProperties.ContentType, Is.EqualTo("application/json"));
            Assert.That(publishedProperties.Type, Is.EqualTo("default-type"));
        });
    }

    [Test]
    public void RedisPrivateHelpers_CoverNullAndUnsupportedBranches()
    {
        var senderProtocol = new QaaS.Framework.Protocols.Protocols.RedisProtocol(new RedisSenderConfig
        {
            HostNames = ["localhost:6379"],
            RedisDataType = RedisDataType.SetString
        }, Globals.Logger);
        Assert.Throws<InvalidOperationException>(() => senderProtocol.SendChunk([]).ToList());

        var addToTransaction = typeof(QaaS.Framework.Protocols.Protocols.RedisProtocol)
            .GetMethod("AddToRedisTransactionByRedisType", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var transaction = new Mock<ITransaction>().Object;

        void InvokeAdd(RedisDataType redisType, MetaData? metaData = null)
        {
            var protocol = new QaaS.Framework.Protocols.Protocols.RedisProtocol(new RedisSenderConfig
            {
                HostNames = ["localhost:6379"],
                RedisDataType = redisType
            }, Globals.Logger);
            addToTransaction.Invoke(protocol, [transaction, new Data<byte[]>
            {
                Body = Encoding.UTF8.GetBytes("value"),
                MetaData = metaData
            }]);
        }

        Assert.Throws<TargetInvocationException>(() => InvokeAdd(RedisDataType.HashSet,
            new MetaData { Redis = new Redis { Key = "k" } }));
        Assert.Throws<TargetInvocationException>(() => InvokeAdd(RedisDataType.SortedSetAdd,
            new MetaData { Redis = new Redis { Key = "k" } }));
        Assert.Throws<TargetInvocationException>(() => InvokeAdd(RedisDataType.GeoAdd,
            new MetaData { Redis = new Redis { Key = "k", GeoLongitude = 1 } }));

        var invalidProtocol = new QaaS.Framework.Protocols.Protocols.RedisProtocol(new RedisSenderConfig
        {
            HostNames = ["localhost:6379"],
            RedisDataType = (RedisDataType)999
        }, Globals.Logger);
        Assert.Throws<TargetInvocationException>(() => addToTransaction.Invoke(invalidProtocol, [transaction, new Data<byte[]>
        {
            Body = Encoding.UTF8.GetBytes("value"),
            MetaData = new MetaData { Redis = new Redis { Key = "k", HashField = "h", SetScore = 1, GeoLatitude = 1, GeoLongitude = 1 } }
        }]));

        var readerDatabase = new Mock<IDatabase>();
        readerDatabase.Setup(database => database.StringGetDelete("string", CommandFlags.None)).Returns(RedisValue.Null);
        readerDatabase.Setup(database => database.ListRightPop("right", CommandFlags.None)).Returns(RedisValue.Null);
        readerDatabase.Setup(database => database.SetPop("set", CommandFlags.None)).Returns(RedisValue.Null);
        readerDatabase.Setup(database => database.HashGet("hash", "field", CommandFlags.None)).Returns(RedisValue.Null);
        readerDatabase.Setup(database => database.SortedSetRangeByRankWithScores("sorted", 0, 0, Order.Ascending, CommandFlags.None))
            .Returns([]);

        DetailedData<object>? InvokeRead(RedisReaderConfig config, string methodName)
        {
            var protocol = new RedisReaderProtocol(config, Globals.Logger);
            SetPrivateField(protocol, "_redisDb", readerDatabase.Object);
            return (DetailedData<object>?)typeof(RedisReaderProtocol)
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(protocol, null);
        }

        var createDetailedData = typeof(RedisReaderProtocol).GetMethod("CreateDetailedData",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var invalidReader = new RedisReaderProtocol(new RedisReaderConfig
        {
            HostNames = ["localhost:6379"],
            Key = "invalid",
            RedisDataType = (RedisDataType)999,
            PollIntervalMs = 1
        }, Globals.Logger);
        SetPrivateField(invalidReader, "_redisDb", readerDatabase.Object);

        Assert.Multiple(() =>
        {
            Assert.That(InvokeRead(new RedisReaderConfig
            {
                HostNames = ["localhost:6379"],
                Key = "string",
                RedisDataType = RedisDataType.SetString
            }, "ReadString"), Is.Null);
            Assert.That(InvokeRead(new RedisReaderConfig
            {
                HostNames = ["localhost:6379"],
                Key = "right",
                RedisDataType = RedisDataType.ListRightPush
            }, "ReadListRight"), Is.Null);
            Assert.That(InvokeRead(new RedisReaderConfig
            {
                HostNames = ["localhost:6379"],
                Key = "set",
                RedisDataType = RedisDataType.SetAdd
            }, "ReadSet"), Is.Null);
            Assert.That(InvokeRead(new RedisReaderConfig
            {
                HostNames = ["localhost:6379"],
                Key = "hash",
                HashField = "field",
                RedisDataType = RedisDataType.HashSet
            }, "ReadHash"), Is.Null);
            Assert.That(InvokeRead(new RedisReaderConfig
            {
                HostNames = ["localhost:6379"],
                Key = "sorted",
                RedisDataType = RedisDataType.SortedSetAdd
            }, "ReadSortedSet"), Is.Null);
            Assert.That(createDetailedData.Invoke(null, [RedisValue.Null, new Redis { Key = "k" }]), Is.Null);
            Assert.Throws<InvalidOperationException>(() => new RedisReaderProtocol(new RedisReaderConfig
            {
                HostNames = ["localhost:6379"],
                Key = "missing",
                RedisDataType = RedisDataType.SetString
            }, Globals.Logger).Read(TimeSpan.FromMilliseconds(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => invalidReader.Read(TimeSpan.FromMilliseconds(1)));
        });
    }
}
