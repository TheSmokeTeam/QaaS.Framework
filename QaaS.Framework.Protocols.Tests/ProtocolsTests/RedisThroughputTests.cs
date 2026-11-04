using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QaaS.Framework.Protocols.ConfigurationObjects.Redis;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using StackExchange.Redis;
using RedisProtocol = QaaS.Framework.Protocols.Protocols.RedisProtocol;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class RedisThroughputTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void CommandFailure_IsObserved_WithoutReplayingSuccessfulSiblings(bool transactional)
    {
        var transaction = new Mock<ITransaction>();
        transaction.Setup(x => x.ExecuteAsync(It.IsAny<CommandFlags>())).ReturnsAsync(true);
        var batch = new Mock<IBatch>();
        var failure = new RedisException("WRONGTYPE");
        transaction
            .Setup(x =>
                x.SetAddAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .Returns(Task.FromException<bool>(failure));
        batch
            .Setup(x =>
                x.SetAddAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .Returns(Task.FromException<bool>(failure));
        var database = new Mock<IDatabase>();
        database.Setup(x => x.CreateTransaction(It.IsAny<object?>())).Returns(transaction.Object);
        database.Setup(x => x.CreateBatch(It.IsAny<object?>())).Returns(batch.Object);
        var protocol = new RedisProtocol(
            new RedisSenderConfig
            {
                RedisDataType = RedisDataType.SetAdd,
                UseTransactions = transactional,
                Retries = 3,
                RetryIntervalMs = 0,
            },
            NullLogger.Instance,
            database.Object
        );
        Assert.Throws<RedisException>(() =>
            protocol
                .SendChunk([
                    new Data<object>
                    {
                        Body = new byte[] { 1 },
                        MetaData = new MetaData { Redis = new Redis { Key = "items" } },
                    },
                ])
                .ToArray()
        );
        transaction.Verify(
            x => x.ExecuteAsync(It.IsAny<CommandFlags>()),
            transactional ? Times.Once() : Times.Never()
        );
        batch.Verify(x => x.Execute(), transactional ? Times.Never() : Times.Once());
    }
}
