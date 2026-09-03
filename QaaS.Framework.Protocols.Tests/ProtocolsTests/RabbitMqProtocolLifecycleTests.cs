using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QaaS.Framework.Protocols.ConfigurationObjects.RabbitMq;
using QaaS.Framework.Protocols.Protocols;
using RabbitMQ.Client;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class RabbitMqProtocolLifecycleTests
{
    private static void SetPrivateField<TValue>(object instance, string fieldName, TValue value)
    {
        instance
            .GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(instance, value);
    }

    private static void SetPrivateProperty<TValue>(object instance, string propertyName, TValue value)
    {
        instance
            .GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
            .SetValue(instance, value);
    }

    private static (Mock<IConnectionFactory> factoryMock, Mock<IConnection> connMock, Mock<IChannel> chanMock) CreateMocks()
    {
        var channelMock = new Mock<IChannel>();
        channelMock
            .Setup(c => c.QueueDeclareAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("ok", 0, 0));

        channelMock
            .Setup(c => c.QueueBindAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object?>?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        channelMock
            .Setup(c => c.QueueDeleteAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0u);

        channelMock
            .Setup(c => c.CloseAsync(
                It.IsAny<ushort>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var connectionMock = new Mock<IConnection>();
        connectionMock
            .Setup(c => c.CreateChannelAsync(
                It.IsAny<CreateChannelOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        connectionMock
            .Setup(c => c.CloseAsync(
                It.IsAny<ushort>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var connectionFactoryMock = new Mock<IConnectionFactory>();
        connectionFactoryMock
            .Setup(cf => cf.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connectionMock.Object);

        return (connectionFactoryMock, connectionMock, channelMock);
    }

    [Test]
    public void Connect_AnonymousQueue_DeclaresWithDefaultExpirationAndBindsToExchange()
    {
        var (factoryMock, _, channelMock) = CreateMocks();
        var config = new RabbitMqReaderConfig
        {
            Host = "localhost",
            ExchangeName = "events.exchange",
            RoutingKey = "orders.#",
            CreatedQueueTimeToExpireMs = 180000,
        };

        var protocol = new RabbitMqProtocol(config, NullLogger.Instance);
        SetPrivateProperty(protocol, "ConnectionFactory", factoryMock.Object);

        string? declaredQueue = null;
        bool? declaredDurable = null;
        bool? declaredExclusive = null;
        bool? declaredAutoDelete = null;
        IDictionary<string, object?>? declaredArguments = null;

        channelMock
            .Setup(c => c.QueueDeclareAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, bool, bool, bool, IDictionary<string, object?>?, bool, bool, CancellationToken>(
                (queue, durable, exclusive, autoDelete, arguments, _, _, _) =>
                {
                    declaredQueue = queue;
                    declaredDurable = durable;
                    declaredExclusive = exclusive;
                    declaredAutoDelete = autoDelete;
                    declaredArguments = arguments;
                })
            .ReturnsAsync(new QueueDeclareOk("ok", 0, 0));

        protocol.Connect();

        Assert.Multiple(() =>
        {
            Assert.That(declaredQueue, Does.StartWith("QaaS_"));
            Assert.That(declaredDurable, Is.False);
            Assert.That(declaredExclusive, Is.False);
            Assert.That(declaredAutoDelete, Is.False);
            Assert.That(declaredArguments, Is.Not.Null);
            Assert.That(declaredArguments!["x-expires"], Is.EqualTo(180000));
        });

        channelMock.Verify(c => c.QueueBindAsync(
            declaredQueue!,
            "events.exchange",
            "orders.#",
            It.IsAny<IDictionary<string, object?>?>(),
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void Connect_NamedQueue_DeclaresWithConfiguredParametersAndSkipsBinding_PreventingAmqp403()
    {
        var (factoryMock, _, channelMock) = CreateMocks();
        var config = new RabbitMqReaderConfig
        {
            Host = "localhost",
            QueueName = "orders-work-queue",
            Durable = true,
            Exclusive = true,
            AutoDelete = true,
            Arguments = new Dictionary<string, object?>
            {
                ["x-message-ttl"] = 60000,
                ["x-dead-letter-exchange"] = "dlx",
                ["x-max-length"] = 1000
            }
        };

        var protocol = new RabbitMqProtocol(config, NullLogger.Instance);
        SetPrivateProperty(protocol, "ConnectionFactory", factoryMock.Object);

        string? declaredQueue = null;
        bool? declaredDurable = null;
        bool? declaredExclusive = null;
        bool? declaredAutoDelete = null;
        IDictionary<string, object?>? declaredArguments = null;

        channelMock
            .Setup(c => c.QueueDeclareAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, bool, bool, bool, IDictionary<string, object?>?, bool, bool, CancellationToken>(
                (queue, durable, exclusive, autoDelete, arguments, _, _, _) =>
                {
                    declaredQueue = queue;
                    declaredDurable = durable;
                    declaredExclusive = exclusive;
                    declaredAutoDelete = autoDelete;
                    declaredArguments = arguments;
                })
            .ReturnsAsync(new QueueDeclareOk("orders-work-queue", 0, 0));

        protocol.Connect();

        Assert.Multiple(() =>
        {
            Assert.That(declaredQueue, Is.EqualTo("orders-work-queue"));
            Assert.That(declaredDurable, Is.True);
            Assert.That(declaredExclusive, Is.True);
            Assert.That(declaredAutoDelete, Is.True);
            Assert.That(declaredArguments, Is.Not.Null);
            Assert.That(declaredArguments!["x-message-ttl"], Is.EqualTo(60000));
            Assert.That(declaredArguments["x-dead-letter-exchange"], Is.EqualTo("dlx"));
            Assert.That(declaredArguments["x-max-length"], Is.EqualTo(1000));
            Assert.That(declaredArguments.ContainsKey("x-expires"), Is.False, "x-expires must not be added to named queues");
        });

        // Verifies AMQP 403 prevention: QueueBindAsync MUST NOT be called on default/empty exchange
        channelMock.Verify(c => c.QueueBindAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IDictionary<string, object?>?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void Connect_AnonymousQueue_MergesUserSuppliedArgumentsWithDefaultExpires()
    {
        var (factoryMock, _, channelMock) = CreateMocks();
        var config = new RabbitMqReaderConfig
        {
            Host = "localhost",
            ExchangeName = "my-exchange",
            Arguments = new Dictionary<string, object?>
            {
                ["x-max-priority"] = 10
            }
        };

        var protocol = new RabbitMqProtocol(config, NullLogger.Instance);
        SetPrivateProperty(protocol, "ConnectionFactory", factoryMock.Object);

        IDictionary<string, object?>? declaredArguments = null;
        channelMock
            .Setup(c => c.QueueDeclareAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, bool, bool, bool, IDictionary<string, object?>?, bool, bool, CancellationToken>(
                (_, _, _, _, arguments, _, _, _) => declaredArguments = arguments)
            .ReturnsAsync(new QueueDeclareOk("ok", 0, 0));

        protocol.Connect();

        Assert.Multiple(() =>
        {
            Assert.That(declaredArguments, Is.Not.Null);
            Assert.That(declaredArguments!["x-max-priority"], Is.EqualTo(10));
            Assert.That(declaredArguments["x-expires"], Is.EqualTo(300000));
        });
    }

    [Test]
    public void Connect_AnonymousQueue_PreservesExplicitUserExpires()
    {
        var (factoryMock, _, channelMock) = CreateMocks();
        var config = new RabbitMqReaderConfig
        {
            Host = "localhost",
            ExchangeName = "my-exchange",
            Arguments = new Dictionary<string, object?>
            {
                ["x-expires"] = 45000
            }
        };

        var protocol = new RabbitMqProtocol(config, NullLogger.Instance);
        SetPrivateProperty(protocol, "ConnectionFactory", factoryMock.Object);

        IDictionary<string, object?>? declaredArguments = null;
        channelMock
            .Setup(c => c.QueueDeclareAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, bool, bool, bool, IDictionary<string, object?>?, bool, bool, CancellationToken>(
                (_, _, _, _, arguments, _, _, _) => declaredArguments = arguments)
            .ReturnsAsync(new QueueDeclareOk("ok", 0, 0));

        protocol.Connect();

        Assert.Multiple(() =>
        {
            Assert.That(declaredArguments, Is.Not.Null);
            Assert.That(declaredArguments!["x-expires"], Is.EqualTo(45000));
        });
    }

    [Test]
    public void Disconnect_AnonymousQueueReader_DeletesDefaultQueue()
    {
        var (_, connectionMock, channelMock) = CreateMocks();
        var config = new RabbitMqReaderConfig
        {
            Host = "localhost",
            ExchangeName = "events",
        };

        var protocol = new RabbitMqProtocol(config, NullLogger.Instance);
        SetPrivateField(protocol, "_channel", channelMock.Object);
        SetPrivateField(protocol, "_connection", connectionMock.Object);

        var defaultQueueName = (string)typeof(RabbitMqProtocol)
            .GetField("_defaultQueueName", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(protocol)!;

        protocol.Disconnect();

        channelMock.Verify(c => c.QueueDeleteAsync(defaultQueueName, false, false, false, It.IsAny<CancellationToken>()), Times.Once);
        channelMock.Verify(c => c.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        connectionMock.Verify(c => c.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void Disconnect_NamedQueueReader_DoesNotDeleteQueue_PreventingAmqp404()
    {
        var (_, connectionMock, channelMock) = CreateMocks();
        var config = new RabbitMqReaderConfig
        {
            Host = "localhost",
            QueueName = "persistent-queue",
        };

        var protocol = new RabbitMqProtocol(config, NullLogger.Instance);
        SetPrivateField(protocol, "_channel", channelMock.Object);
        SetPrivateField(protocol, "_connection", connectionMock.Object);

        protocol.Disconnect();

        // Must NOT attempt to delete default queue or named queue on disconnect
        channelMock.Verify(c => c.QueueDeleteAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        channelMock.Verify(c => c.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        connectionMock.Verify(c => c.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void Disconnect_Sender_DoesNotDeleteQueue()
    {
        var (_, connectionMock, channelMock) = CreateMocks();
        var config = new RabbitMqSenderConfig
        {
            Host = "localhost",
            QueueName = "target-queue",
        };

        var protocol = new RabbitMqProtocol(config, NullLogger.Instance);
        SetPrivateField(protocol, "_channel", channelMock.Object);
        SetPrivateField(protocol, "_connection", connectionMock.Object);

        protocol.Disconnect();

        channelMock.Verify(c => c.QueueDeleteAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        channelMock.Verify(c => c.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        connectionMock.Verify(c => c.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
