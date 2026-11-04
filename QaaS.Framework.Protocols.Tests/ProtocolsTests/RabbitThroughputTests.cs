using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QaaS.Framework.Protocols.ConfigurationObjects.RabbitMq;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.SDK.Session.DataObjects;
using RabbitMQ.Client;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class RabbitThroughputTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void Disconnect_ClosesConnectionWhenSubscriptionCleanupFails(bool failChannelClose)
    {
        var connection = new Mock<IConnection>();
        var (factory, channel) = Connection(connection);
        CaptureSubscription(channel);
        if (failChannelClose)
            channel
                .Setup(x =>
                    x.CloseAsync(
                        It.IsAny<ushort>(),
                        It.IsAny<string>(),
                        It.IsAny<bool>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ThrowsAsync(new IOException("Channel close failed"));
        else
            channel
                .Setup(x =>
                    x.BasicCancelAsync(
                        "subscription",
                        It.IsAny<bool>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ThrowsAsync(new IOException("Subscription cancellation failed"));
        using var protocol = new RabbitMqProtocol(
            new RabbitMqReaderConfig
            {
                Host = "localhost",
                QueueName = "queue",
                UseSubscription = true,
            },
            NullLogger.Instance,
            factory.Object
        );
        protocol.Connect();
        Assert.Throws<IOException>(() => protocol.Disconnect());
        channel.Verify(
            x =>
                x.CloseAsync(
                    It.IsAny<ushort>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        connection.Verify(
            x =>
                x.CloseAsync(
                    It.IsAny<ushort>(),
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public void Polling_PropagatesRpcCancellationAfterTheReadDeadline()
    {
        var (factory, channel) = Connection();
        channel
            .Setup(x => x.BasicGetAsync("queue", true, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(100);
                throw new OperationCanceledException("RPC continuation timed out");
            });
        using var protocol = new RabbitMqProtocol(
            new RabbitMqReaderConfig { Host = "localhost", QueueName = "queue" },
            NullLogger.Instance,
            factory.Object
        );
        protocol.Connect();
        Assert.Throws<OperationCanceledException>(() =>
            protocol.Read(TimeSpan.FromMilliseconds(50))
        );
    }

    [Test]
    public async Task Subscription_CompletesAcknowledgementAfterTheReadDeadline()
    {
        var (factory, channel) = Connection();
        var consumer = CaptureSubscription(channel);
        channel
            .Setup(x => x.BasicAckAsync(42, false, It.IsAny<CancellationToken>()))
            .Returns(
                (ulong _, bool _, CancellationToken token) => new ValueTask(Task.Delay(100, token))
            );
        using var protocol = new RabbitMqProtocol(
            new RabbitMqReaderConfig
            {
                Host = "localhost",
                QueueName = "queue",
                UseSubscription = true,
            },
            NullLogger.Instance,
            factory.Object
        );
        protocol.Connect();
        await consumer()!
            .HandleBasicDeliverAsync(
                "subscription",
                42,
                false,
                "",
                "queue",
                new BasicProperties(),
                new byte[] { 7 },
                default
            );
        Assert.That(
            protocol.Read(TimeSpan.FromMilliseconds(50))?.Body,
            Is.EqualTo(new byte[] { 7 })
        );
    }

    [Test]
    public async Task Subscription_BrokerCancellationFailsTheRead()
    {
        var (factory, channel) = Connection();
        var consumer = CaptureSubscription(channel);
        using var protocol = new RabbitMqProtocol(
            new RabbitMqReaderConfig
            {
                Host = "localhost",
                QueueName = "queue",
                UseSubscription = true,
            },
            NullLogger.Instance,
            factory.Object
        );
        protocol.Connect();
        await consumer()!.HandleBasicCancelAsync("subscription", default);
        var error = Assert.Throws<System.Threading.Channels.ChannelClosedException>(() =>
            protocol.Read(TimeSpan.FromMilliseconds(100))
        );
        Assert.That(error!.InnerException, Is.TypeOf<IOException>());
    }

    private static Func<IAsyncBasicConsumer?> CaptureSubscription(Mock<IChannel> channel)
    {
        IAsyncBasicConsumer? consumer = null;
        channel
            .Setup(x =>
                x.BasicConsumeAsync(
                    It.IsAny<string>(),
                    false,
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<IDictionary<string, object?>?>(),
                    It.IsAny<IAsyncBasicConsumer>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                string,
                bool,
                string,
                bool,
                bool,
                IDictionary<string, object?>?,
                IAsyncBasicConsumer,
                CancellationToken
            >((_, _, _, _, _, _, value, _) => consumer = value)
            .ReturnsAsync("subscription");
        return () => consumer;
    }

    private static (Mock<IConnectionFactory> Factory, Mock<IChannel> Channel) Connection(
        Mock<IConnection>? connection = null
    )
    {
        var channel = new Mock<IChannel>();
        channel
            .Setup(x =>
                x.QueueDeclareAsync(
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<IDictionary<string, object?>?>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new QueueDeclareOk("queue", 0, 0));
        connection ??= new Mock<IConnection>();
        connection
            .Setup(x =>
                x.CreateChannelAsync(
                    It.IsAny<CreateChannelOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(channel.Object);
        var factory = new Mock<IConnectionFactory>();
        factory
            .Setup(x => x.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection.Object);
        return (factory, channel);
    }

    [TestCase(true, 3)]
    [TestCase(false, 1)]
    public void TopologyValidation_DefaultPerSend_OptInOnce(bool everyOperation, int checks)
    {
        var (factory, channel) = Connection();
        using var protocol = new RabbitMqProtocol(
            new RabbitMqSenderConfig
            {
                Host = "localhost",
                ExchangeName = "events",
                ValidateTopologyOnEveryOperation = everyOperation,
            },
            NullLogger.Instance,
            factory.Object
        );
        protocol.Connect();
        for (var i = 0; i < 3; i++)
            protocol.Send(new Data<object> { Body = new byte[] { 1 } });
        channel.Verify(
            x => x.ExchangeDeclarePassiveAsync("events", It.IsAny<CancellationToken>()),
            Times.Exactly(checks)
        );
    }

    [Test]
    public async Task Subscription_CopiesPayload_AcknowledgesOnlyWhenRead_AndCancelsOnDisconnect()
    {
        var (factory, channel) = Connection();
        IAsyncBasicConsumer? consumer = null;
        channel
            .Setup(x =>
                x.BasicConsumeAsync(
                    It.IsAny<string>(),
                    false,
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<IDictionary<string, object?>?>(),
                    It.IsAny<IAsyncBasicConsumer>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                string,
                bool,
                string,
                bool,
                bool,
                IDictionary<string, object?>?,
                IAsyncBasicConsumer,
                CancellationToken
            >((_, _, _, _, _, _, value, _) => consumer = value)
            .ReturnsAsync("subscription");
        using var protocol = new RabbitMqProtocol(
            new RabbitMqReaderConfig
            {
                Host = "localhost",
                QueueName = "queue",
                UseSubscription = true,
                PrefetchCount = 2,
            },
            NullLogger.Instance,
            factory.Object
        );
        protocol.Connect();
        var bytes = new byte[] { 7, 8 };
        await consumer!.HandleBasicDeliverAsync(
            "subscription",
            42,
            false,
            "",
            "queue",
            new BasicProperties(),
            bytes,
            default
        );
        bytes[0] = 99;
        channel.Verify(
            x =>
                x.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        var result = protocol.Read(TimeSpan.FromSeconds(1));
        Assert.That(result!.Body, Is.EqualTo(new byte[] { 7, 8 }));
        channel.Verify(x => x.BasicAckAsync(42, false, It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(
            x => x.BasicQosAsync(0, 2, false, It.IsAny<CancellationToken>()),
            Times.Once
        );
        channel.Verify(
            x =>
                x.BasicGetAsync(
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        protocol.Disconnect();
        channel.Verify(
            x =>
                x.BasicCancelAsync("subscription", It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
