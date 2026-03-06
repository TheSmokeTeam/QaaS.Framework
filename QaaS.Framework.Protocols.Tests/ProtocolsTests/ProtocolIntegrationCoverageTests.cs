using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using Confluent.Kafka;
using Google.Protobuf;
using Grpc.Core;
using Moq;
using QaaS.Framework.Protocols.ConfigurationObjects.Elastic;
using QaaS.Framework.Protocols.ConfigurationObjects.Grpc;
using QaaS.Framework.Protocols.ConfigurationObjects.Kafka;
using QaaS.Framework.Protocols.ConfigurationObjects.Prometheus;
using QaaS.Framework.Protocols.ConfigurationObjects.RabbitMq;
using QaaS.Framework.Protocols.ConfigurationObjects.Redis;
using QaaS.Framework.Protocols.ConfigurationObjects.Sftp;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.SDK.Session;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Renci.SshNet;
using StackExchange.Redis;
using StringValue = Google.Protobuf.WellKnownTypes.StringValue;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class ProtocolIntegrationCoverageTests
{
    private sealed class ExposedPrometheusProtocol(PrometheusFetcherConfig fetcherConfig)
        : PrometheusProtocol(fetcherConfig, Globals.Logger)
    {
        public string InvokeHttpGetResultBodyAsString(string queryRequestUri) =>
            HttpGetResultBodyAsString(queryRequestUri);
    }

    [Test]
    public void KafkaTopicProtocol_Send_Read_And_Lifecycle_AreCovered()
    {
        var senderConfig = new KafkaTopicSenderConfig
        {
            HostNames = ["localhost:9092"],
            Username = "user",
            Password = "pass",
            TopicName = "topic-default",
            Partition = 0,
            MessageSendMaxRetries = 2,
            MessageSendRetriesIntervalMs = 0,
            Headers = new Dictionary<string, object?> { ["h1"] = "v1" },
            DefaultKafkaKey = "default-key"
        };

        var sender = new KafkaTopicProtocol(senderConfig, Globals.Logger);
        var producerMock = new Mock<IProducer<byte[]?, byte[]?>>();

        var produceAttempts = 0;
        producerMock
            .Setup(mock => mock.Produce(It.IsAny<TopicPartition>(), It.IsAny<Message<byte[]?, byte[]?>>(),
                It.IsAny<Action<DeliveryReport<byte[]?, byte[]?>>?>()))
            .Callback(() =>
            {
                produceAttempts++;
                if (produceAttempts == 1)
                    throw new KafkaException(new Error(ErrorCode.Local_MsgTimedOut));
            });

        SetPrivateField(sender, "_producer", producerMock.Object);

        var sent = sender.Send(new Data<object>
        {
            Body = "payload"u8.ToArray(),
            MetaData = new MetaData
            {
                Kafka = new Kafka
                {
                    TopicName = "topic-override",
                    Headers = new Dictionary<string, object?> { ["h2"] = "v2" }
                }
            }
        });

        sender.Disconnect();
        sender.Dispose();

        Assert.That(sent.Body, Is.TypeOf<byte[]>());
        Assert.That(produceAttempts, Is.EqualTo(2));
        producerMock.Verify(mock => mock.Flush(It.IsAny<CancellationToken>()), Times.Once);
        producerMock.Verify(mock => mock.Dispose(), Times.Once);

        var readerConfig = new KafkaTopicReaderConfig
        {
            HostNames = ["localhost:9092"],
            Username = "user",
            Password = "pass",
            TopicName = "topic-default",
            GroupId = "group"
        };

        var reader = new KafkaTopicProtocol(readerConfig, Globals.Logger);
        var consumerMock = new Mock<IConsumer<byte[]?, byte[]?>>();

        var consumed = new ConsumeResult<byte[]?, byte[]?>
        {
            Message = new Message<byte[]?, byte[]?>
            {
                Key = "key"u8.ToArray(),
                Value = "value"u8.ToArray(),
                Timestamp = new Timestamp(DateTime.UtcNow)
            },
            TopicPartitionOffset = new TopicPartitionOffset("topic-default", new Partition(0), new Offset(1))
        };

        consumerMock.SetupSequence(mock => mock.Consume(It.IsAny<TimeSpan>()))
            .Returns((ConsumeResult<byte[]?, byte[]?>)null!)
            .Returns(consumed);

        SetPrivateField(reader, "_consumer", consumerMock.Object);

        reader.Connect();
        var noData = reader.Read(TimeSpan.FromMilliseconds(1));
        var withData = reader.Read(TimeSpan.FromMilliseconds(1));
        reader.Disconnect();
        reader.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(noData, Is.Null);
            Assert.That(withData, Is.Not.Null);
            Assert.That(withData!.Body, Is.TypeOf<byte[]>());
            Assert.That(reader.GetSerializationType(), Is.Null);
        });

        consumerMock.Verify(mock => mock.Subscribe("topic-default"), Times.Once);
        consumerMock.Verify(mock => mock.Commit(consumed), Times.Once);
        consumerMock.Verify(mock => mock.Unsubscribe(), Times.Once);
        consumerMock.Verify(mock => mock.Dispose(), Times.Once);
    }

    [Test]
    public void KafkaTopicProtocol_Send_UsesDefaultTopicAndNullHeaders_WhenNoMetadataHeaders()
    {
        var sender = new KafkaTopicProtocol(new KafkaTopicSenderConfig
        {
            HostNames = ["localhost:9092"],
            Username = "user",
            Password = "pass",
            TopicName = "topic-default",
            Partition = 0,
            MessageSendMaxRetries = 1,
            MessageSendRetriesIntervalMs = 0,
            DefaultKafkaKey = "fallback-key",
        }, Globals.Logger);

        var producerMock = new Mock<IProducer<byte[]?, byte[]?>>();
        TopicPartition? sentPartition = null;
        Message<byte[]?, byte[]?>? sentMessage = null;
        producerMock
            .Setup(mock => mock.Produce(It.IsAny<TopicPartition>(), It.IsAny<Message<byte[]?, byte[]?>>(),
                It.IsAny<Action<DeliveryReport<byte[]?, byte[]?>>?>()))
            .Callback<TopicPartition, Message<byte[]?, byte[]?>, Action<DeliveryReport<byte[]?, byte[]?>>?>((
                topicPartition, message, _) =>
            {
                sentPartition = topicPartition;
                sentMessage = message;
            });
        SetPrivateField(sender, "_producer", producerMock.Object);

        sender.Connect(); // sender has no consumer, should be no-op branch
        var sent = sender.Send(new Data<object> { Body = "payload"u8.ToArray(), MetaData = null });

        Assert.Multiple(() =>
        {
            Assert.That(sent.Body, Is.TypeOf<byte[]>());
            Assert.That(sentPartition, Is.Not.Null);
            Assert.That(sentPartition!.Topic, Is.EqualTo("topic-default"));
            Assert.That(sentMessage, Is.Not.Null);
            Assert.That(sentMessage!.Headers, Is.Null);
            Assert.That(Encoding.UTF8.GetString(sentMessage.Key!), Is.EqualTo("fallback-key"));
        });
    }

    [Test]
    public void KafkaTopicProtocol_Send_WhenRetriesExhausted_ThrowsKafkaException()
    {
        var sender = new KafkaTopicProtocol(new KafkaTopicSenderConfig
        {
            HostNames = ["localhost:9092"],
            Username = "user",
            Password = "pass",
            TopicName = "topic-default",
            Partition = 0,
            MessageSendMaxRetries = 2,
            MessageSendRetriesIntervalMs = 0
        }, Globals.Logger);

        var producerMock = new Mock<IProducer<byte[]?, byte[]?>>();
        producerMock
            .Setup(mock => mock.Produce(It.IsAny<TopicPartition>(), It.IsAny<Message<byte[]?, byte[]?>>(),
                It.IsAny<Action<DeliveryReport<byte[]?, byte[]?>>?>()))
            .Throws(new KafkaException(new Error(ErrorCode.Local_MsgTimedOut)));
        SetPrivateField(sender, "_producer", producerMock.Object);

        Assert.Throws<KafkaException>(() =>
            sender.Send(new Data<object> { Body = "payload"u8.ToArray(), MetaData = null }));
    }
    [TestCase("test-key:test-value", 1, "test-key", "test-value")]
    [TestCase("trace:old,trace:new", 1, "trace", "new")]
    [TestCase("auth:secret,id:1,id:2", 2, "id", "2")]
    public void KafkaTopicProtocol_Read_HeaderScenarios(string headerInput, int expectedCount, string keyToVerify,
        string expectedValue)
    {
        var readerConfig = new KafkaTopicReaderConfig
        {
            HostNames = ["localhost:9092"],
            Username = "user",
            Password = "pass",
            TopicName = "topic-default",
            GroupId = "group"
        };
        var reader = new KafkaTopicProtocol(readerConfig, Globals.Logger);
        var consumerMock = new Mock<IConsumer<byte[]?, byte[]?>>();
        var kafkaHeaders = new Confluent.Kafka.Headers();
        foreach (var pair in headerInput.Split(','))
        {
            var parts = pair.Split(':');
            kafkaHeaders.Add(parts[0], Encoding.UTF8.GetBytes(parts[1]));
        }

        var consumed = new ConsumeResult<byte[]?, byte[]?>
        {
            Message = new Message<byte[]?, byte[]?>
            {
                Key = "key"u8.ToArray(),
                Value = "value"u8.ToArray(),
                Headers = kafkaHeaders
            }
            }
        };

        consumerMock.Setup(m => m.Consume(It.IsAny<TimeSpan>())).Returns(consumed);
        SetPrivateField(reader, "_consumer", consumerMock.Object);

        reader.Connect();
        var detailedData = reader.Read(TimeSpan.Zero);
        reader.Disconnect();
        reader.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(detailedData, Is.Not.Null);
            Assert.That(detailedData?.Body, Is.EqualTo("value"u8.ToArray()));
            Assert.That(detailedData?.MetaData?.Kafka?.Headers?.Count, Is.EqualTo(expectedCount));
            Assert.That(detailedData?.MetaData?.Kafka?.Headers?[keyToVerify], Is.EqualTo(expectedValue));
        });
        
        consumerMock.Verify(mock => mock.Subscribe("topic-default"), Times.Once);
        consumerMock.Verify(mock => mock.Commit(consumed), Times.Once);
        consumerMock.Verify(mock => mock.Unsubscribe(), Times.Once);
        consumerMock.Verify(mock => mock.Dispose(), Times.Once);
    }

    [Test]
    public void SftpProtocol_UsesConfiguredClient_For_Send_And_Lifecycle()
    {
        var protocol = new SftpProtocol(new SftpSenderConfig
        {
            Hostname = "127.0.0.1",
            Port = 22,
            Username = "u",
            Password = "p",
            Path = "/tmp",
            Prefix = "pref-"
        }, Globals.Logger);

        var producerMock = new Mock<ISftpClient>();
        string? capturedPath = null;
        byte[]? capturedBody = null;

        producerMock
            .Setup(mock => mock.WriteAllBytes(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Callback<string, byte[]>((path, body) =>
            {
                capturedPath = path;
                capturedBody = body;
            });

        SetPrivateField(protocol, "_producer", producerMock.Object);

        protocol.Connect();
        var sent = protocol.Send(new Data<object>
        {
            Body = "file-content"u8.ToArray(),
            MetaData = new MetaData
            {
                Storage = new Storage { Key = "file.bin" }
            }
        });
        protocol.Disconnect();

        Assert.Multiple(() =>
        {
            Assert.That(protocol.GetSerializationType(), Is.Null);
            Assert.That(sent.Body, Is.TypeOf<byte[]>());
            Assert.That(capturedPath, Is.EqualTo(Path.Combine("/tmp", "file.bin")));
            Assert.That(capturedBody, Is.EqualTo("file-content"u8.ToArray()));
        });

        producerMock.Verify(mock => mock.Connect(), Times.Once);
        producerMock.Verify(mock => mock.Disconnect(), Times.Once);
    }

    [Test]
    public void RabbitMqProtocol_Send_Read_Disconnect_And_Dispose_AreCovered()
    {
        var channelMock = new Mock<IChannel>();
        channelMock
            .Setup(mock => mock.ExchangeDeclarePassiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        channelMock
            .Setup(mock => mock.BasicPublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        channelMock
            .Setup(mock => mock.QueueDeclarePassiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("q", 0, 0));

        var message = new BasicGetResult(
            deliveryTag: 1,
            redelivered: false,
            exchange: "ex",
            routingKey: "rk",
            messageCount: 0,
            basicProperties: new BasicProperties
            {
                ContentType = "application/octet-stream",
                Expiration = "1000",
                Type = "type",
                Headers = new Dictionary<string, object?> { ["h"] = "v" }
            },
            body: "rabbit-body"u8.ToArray());

        channelMock
            .SetupSequence(mock => mock.BasicGetAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BasicGetResult?)null)
            .ReturnsAsync(message);

        channelMock
            .Setup(mock => mock.QueueDeleteAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<uint>(0));
        channelMock
            .Setup(mock =>
                mock.CloseAsync(It.IsAny<ShutdownEventArgs>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var connectionMock = new Mock<IConnection>();

        var sender = new RabbitMqProtocol(new RabbitMqSenderConfig
        {
            Host = "localhost",
            QueueName = "q",
            RoutingKey = "rk",
            ExchangeName = "ex"
        }, Globals.Logger);
        SetPrivateField(sender, "_channel", channelMock.Object);
        SetPrivateField(sender, "_connection", connectionMock.Object);

        var sent = sender.Send(new Data<object>
        {
            Body = "rabbit-body"u8.ToArray(),
            MetaData = new MetaData
            {
                RabbitMq = new RabbitMq
                {
                    RoutingKey = "rk"
                }
            }
        });

        sender.Disconnect();
        sender.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(sender.GetSerializationType(), Is.Null);
            Assert.That(sent.Body, Is.TypeOf<byte[]>());
        });

        channelMock.Verify(mock => mock.BasicPublishAsync(It.IsAny<string>(), It.IsAny<string>(), true,
            It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()), Times.Once);
        channelMock.Verify(mock => mock.QueueDeleteAsync(It.IsAny<string>(), false, false, false,
            It.IsAny<CancellationToken>()), Times.Once);
        channelMock.Verify(mock => mock.Dispose(), Times.Once);
        connectionMock.Verify(mock => mock.Dispose(), Times.Once);
    }

    [Test]
    public void RedisReaderProtocol_ReadString_UsesAtomicReadAndDelete()
    {
        var protocol = new RedisReaderProtocol(new RedisReaderConfig
        {
            HostNames = ["localhost:6379"],
            Key = "items",
            RedisDataType = RedisDataType.SetString
        }, Globals.Logger);

        var redisDbMock = new Mock<IDatabase>();
        redisDbMock
            .Setup(mock => mock.StringGetDelete("items", It.IsAny<CommandFlags>()))
            .Returns((RedisValue)Encoding.UTF8.GetBytes("value"));

        SetPrivateField(protocol, "_redisDb", redisDbMock.Object);

        var result = protocol.Read(TimeSpan.FromMilliseconds(1));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Body, Is.TypeOf<byte[]>());
            Assert.That(result.MetaData!.Redis!.Key, Is.EqualTo("items"));
        });

        redisDbMock.Verify(mock => mock.StringGetDelete("items", It.IsAny<CommandFlags>()), Times.Once);
        redisDbMock.Verify(mock => mock.KeyDelete(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Test]
    public void GrpcProtocol_Transact_Success_Timeout_And_InvalidRpc()
    {
        var assemblyName = typeof(FakeGrpcService).Assembly.GetName().Name!;

        var config = new GrpcTransactorConfig
        {
            Host = "localhost",
            Port = 5001,
            AssemblyName = assemblyName,
            ProtoNameSpace = "QaaS.Framework.Protocols.Tests.ProtocolsTests",
            ServiceName = nameof(FakeGrpcService),
            RpcName = nameof(FakeGrpcService.FakeGrpcServiceClient.Echo)
        };

        var protocol = new GrpcProtocol(config, Globals.Logger, TimeSpan.FromMilliseconds(100));
        var success = protocol.Transact(new Data<object> { Body = new StringValue { Value = "hello" } });

        Assert.Multiple(() =>
        {
            Assert.That(protocol.GetInputCommunicationSerializationType(),
                Is.EqualTo(Serialization.SerializationType.ProtobufMessage));
            Assert.That(protocol.GetOutputCommunicationSerializationType(),
                Is.EqualTo(Serialization.SerializationType.ProtobufMessage));
            Assert.That(success.Item2, Is.Not.Null);
            Assert.That(((StringValue)success.Item2!.Body!).Value, Is.EqualTo("hello"));
        });

        var timeoutConfig = config with { RpcName = nameof(FakeGrpcService.FakeGrpcServiceClient.Timeout) };
        var timeoutProtocol = new GrpcProtocol(timeoutConfig, Globals.Logger, TimeSpan.FromMilliseconds(1));
        var timeout = timeoutProtocol.Transact(new Data<object> { Body = new StringValue { Value = "late" } });

        Assert.That(timeout.Item2, Is.Null);

        Assert.Throws<ArgumentException>(() => new GrpcProtocol(config with { RpcName = "MissingRpc" }, Globals.Logger,
            TimeSpan.FromMilliseconds(100)));

        Assert.Throws<ArgumentException>(() =>
            protocol.Transact(new Data<object> { Body = null }));
    }

    [Test]
    public void PrometheusProtocol_HttpGetResultBodyAsString_CoversSuccessAndFailure()
    {
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        var serverTask = Task.Run(async () =>
        {
            var okContext = await listener.GetContextAsync();
            Assert.That(okContext.Request.Headers["apikey"], Is.EqualTo("k"));
            okContext.Response.StatusCode = 200;
            await okContext.Response.OutputStream.WriteAsync("{\"ok\":true}"u8.ToArray());
            okContext.Response.Close();

            var badContext = await listener.GetContextAsync();
            badContext.Response.StatusCode = 500;
            await badContext.Response.OutputStream.WriteAsync("{\"error\":true}"u8.ToArray());
            badContext.Response.Close();
        });

        var protocol = new ExposedPrometheusProtocol(new PrometheusFetcherConfig
        {
            Url = $"http://127.0.0.1:{port}",
            Expression = "up",
            ApiKey = "k",
            TimeoutMs = 1000
        });

        var okBody = protocol.InvokeHttpGetResultBodyAsString($"http://127.0.0.1:{port}/ok");
        Assert.That(okBody, Does.Contain("ok"));
        Assert.Throws<HttpRequestException>(() =>
            protocol.InvokeHttpGetResultBodyAsString($"http://127.0.0.1:{port}/bad"));

        serverTask.GetAwaiter().GetResult();
    }

    [Test]
    public void PrometheusProtocol_HttpGetResultBodyAsString_HonorsMillisecondTimeout()
    {
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        var serverTask = Task.Run(async () =>
        {
            var delayedContext = await listener.GetContextAsync();
            await Task.Delay(200);

            try
            {
                delayedContext.Response.StatusCode = 200;
                await delayedContext.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("{\"slow\":true}"));
            }
            catch (HttpListenerException)
            {
                // The client timed out and closed the connection, which is the expected path.
            }
            finally
            {
                try
                {
                    delayedContext.Response.Close();
                }
                catch (HttpListenerException)
                {
                    // The timed-out client can close the socket before the listener finishes disposing the response.
                }
            }
        });

        var protocol = new ExposedPrometheusProtocol(new PrometheusFetcherConfig
        {
            Url = $"http://127.0.0.1:{port}",
            Expression = "up",
            TimeoutMs = 50
        });

        Assert.Throws<TaskCanceledException>(() =>
            protocol.InvokeHttpGetResultBodyAsString($"http://127.0.0.1:{port}/slow"));

        serverTask.GetAwaiter().GetResult();
    }

    [Test]
    public void PrometheusProtocol_Collect_WhenBodyIsInvalidJson_Throws()
    {
        var protocol = new StubPrometheusProtocol(new PrometheusFetcherConfig
        {
            Url = "http://prometheus.local",
            Expression = "up"
        }, "{bad-json");

        Assert.Throws<JsonException>(() => protocol.Collect(DateTime.UtcNow, DateTime.UtcNow).ToList());
    }

    [Test]
    public void ElasticProtocol_Constructors_And_EmptySendChunk_AreCovered()
    {
        var sender = new ElasticProtocol(new ElasticSenderConfig
        {
            Url = "http://localhost:9200",
            Username = "u",
            Password = "p",
            IndexName = "logs-2026"
        }, new DataFilter { Body = true }, Globals.Logger);

        var regex = new ElasticProtocol(new ElasticIndicesRegex
        {
            Url = "http://localhost:9200",
            Username = "u",
            Password = "p",
            IndexPattern = "logs-*"
        }, new DataFilter { Body = true }, Globals.Logger);

        var reader = new ElasticProtocol(new ElasticReaderConfig
        {
            Url = "http://localhost:9200",
            Username = "u",
            Password = "p",
            IndexPattern = "logs-*"
        }, new DataFilter { Body = true }, Globals.Logger);

        var sent = sender.SendChunk([]).ToList();
        sender.Connect();
        sender.Disconnect();
        regex.Connect();
        regex.Disconnect();
        reader.Connect();
        reader.Disconnect();

        Assert.Multiple(() =>
        {
            Assert.That(sent, Is.Empty);
            Assert.That(sender.GetSerializationType(), Is.EqualTo(Serialization.SerializationType.Json));
            Assert.That(regex.GetSerializationType(), Is.EqualTo(Serialization.SerializationType.Json));
            Assert.That(reader.GetSerializationType(), Is.EqualTo(Serialization.SerializationType.Json));
        });
    }

    private static void SetPrivateField<TValue>(object instance, string fieldName, TValue value)
    {
        var fieldInfo = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        fieldInfo!.SetValue(instance, value);
    }

    private sealed class StubPrometheusProtocol(PrometheusFetcherConfig fetcherConfig, string body)
        : PrometheusProtocol(fetcherConfig, Globals.Logger)
    {
        protected override string HttpGetResultBodyAsString(string queryRequestUri) => body;
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

public class FakeGrpcService
{
    public class FakeGrpcServiceClient : ClientBase<FakeGrpcServiceClient>
    {
        public FakeGrpcServiceClient(Channel channel) : base()
        {
        }

        private FakeGrpcServiceClient(ClientBaseConfiguration configuration) : base(configuration)
        {
        }

        protected override FakeGrpcServiceClient NewInstance(ClientBaseConfiguration configuration)
        {
            return new FakeGrpcServiceClient(configuration);
        }

        public IMessage Echo(IMessage request, CallOptions callOptions)
        {
            return request;
        }

        public IMessage Timeout(IMessage request, CallOptions callOptions)
        {
            throw new RpcException(new Status(StatusCode.DeadlineExceeded, "timeout"));
        }
    }
}
