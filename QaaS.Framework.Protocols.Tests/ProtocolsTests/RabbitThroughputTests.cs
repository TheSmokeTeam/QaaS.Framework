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
    private static (Mock<IConnectionFactory> Factory, Mock<IChannel> Channel) Connection()
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
        var connection = new Mock<IConnection>();
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
