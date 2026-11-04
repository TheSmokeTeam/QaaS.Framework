using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QaaS.Framework.Protocols.ConfigurationObjects.Kafka;
using QaaS.Framework.Protocols.Protocols;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class KafkaThroughputTests
{
    private static KafkaTopicReaderConfig Config() =>
        new()
        {
            HostNames = ["localhost:9092"],
            TopicName = "events",
            GroupId = "tests",
        };

    private static ConsumeResult<byte[]?, byte[]?> Record(int partition, long offset) =>
        new()
        {
            Topic = "events",
            Partition = partition,
            Offset = offset,
            Message = new Message<byte[]?, byte[]?>
            {
                Value = [1],
                Timestamp = new Timestamp(DateTime.UtcNow),
            },
        };

    [TestCase(true)]
    [TestCase(false)]
    public void Default_StillCommitsEachReceipt(bool autoCommit)
    {
        var config = Config() with { EnableAutoCommit = autoCommit };
        var consumer = new Mock<IConsumer<byte[]?, byte[]?>>();
        var record = Record(0, 4);
        consumer.Setup(x => x.Consume(It.IsAny<TimeSpan>())).Returns(record);
        using var protocol = new KafkaTopicProtocol(config, NullLogger.Instance, consumer.Object);
        protocol.Read(TimeSpan.Zero);
        consumer.Verify(x => x.Commit(record), Times.Once);
        Assert.That(
            KafkaTopicProtocol.CreateConsumerConfig(config).EnableAutoCommit,
            Is.EqualTo(autoCommit)
        );
    }

    [Test]
    public void Batch_CommitsNextOffsetsPerPartition_AndFlushesOnIdle()
    {
        var config = Config() with { CommitMode = KafkaCommitMode.Batched, CommitBatchSize = 3 };
        var consumer = new Mock<IConsumer<byte[]?, byte[]?>>();
        consumer
            .SetupSequence(x => x.Consume(It.IsAny<TimeSpan>()))
            .Returns(Record(0, 4))
            .Returns(Record(1, 8))
            .Returns(Record(0, 5))
            .Returns(Record(1, 9))
            .Returns((ConsumeResult<byte[]?, byte[]?>)null!);
        var commits = new List<TopicPartitionOffset[]>();
        consumer
            .Setup(x => x.Commit(It.IsAny<IEnumerable<TopicPartitionOffset>>()))
            .Callback<IEnumerable<TopicPartitionOffset>>(x => commits.Add(x.ToArray()));
        using var protocol = new KafkaTopicProtocol(config, NullLogger.Instance, consumer.Object);
        protocol.Read(TimeSpan.Zero);
        protocol.Read(TimeSpan.Zero);
        Assert.That(commits, Is.Empty);
        protocol.Read(TimeSpan.Zero);
        Assert.That(
            commits.Single().Select(x => x.Offset.Value),
            Is.EquivalentTo(new long[] { 6, 9 })
        );
        protocol.Read(TimeSpan.Zero);
        Assert.That(protocol.Read(TimeSpan.Zero), Is.Null);
        Assert.That(commits.Last().Single().Offset.Value, Is.EqualTo(10));
        var native = KafkaTopicProtocol.CreateConsumerConfig(config);
        Assert.Multiple(() =>
        {
            Assert.That(native.EnableAutoCommit, Is.False);
            Assert.That(native.EnableAutoOffsetStore, Is.False);
        });
    }

    [Test]
    public void LostPartitions_AreNotCommitted_AndRevocationOnlyCommitsOwnedPartitions()
    {
        var consumer = new Mock<IConsumer<byte[]?, byte[]?>>();
        consumer
            .SetupSequence(x => x.Consume(It.IsAny<TimeSpan>()))
            .Returns(Record(0, 1))
            .Returns(Record(1, 7));
        var commits = new List<TopicPartitionOffset[]>();
        consumer
            .Setup(x => x.Commit(It.IsAny<IEnumerable<TopicPartitionOffset>>()))
            .Callback<IEnumerable<TopicPartitionOffset>>(x => commits.Add(x.ToArray()));
        using var protocol = new KafkaTopicProtocol(
            Config() with
            {
                CommitMode = KafkaCommitMode.Batched,
            },
            NullLogger.Instance,
            consumer.Object
        );
        protocol.Read(TimeSpan.Zero);
        protocol.Read(TimeSpan.Zero);
        protocol.ForgetPartitions([new TopicPartition("events", 0)]);
        protocol.CommitPending(consumer.Object, [new TopicPartition("events", 1)]);
        Assert.That(commits.Single().Single().Offset.Value, Is.EqualTo(8));
        protocol.Disconnect();
        Assert.That(commits, Has.Count.EqualTo(1));
        consumer.Verify(x => x.Close(), Times.Once);
    }

    [Test]
    public void ProducerOptions_AreUnspecifiedUnlessOptedIn()
    {
        var config = new KafkaTopicSenderConfig { HostNames = ["localhost:9092"] };
        var native = KafkaTopicProtocol.CreateProducerConfig(config);
        Assert.Multiple(() =>
        {
            Assert.That(native.LingerMs, Is.Null);
            Assert.That(native.BatchSize, Is.Null);
            Assert.That(native.Acks, Is.Null);
            Assert.That(native.EnableIdempotence, Is.Null);
        });
        config.LingerMs = 5;
        config.BatchSize = 65536;
        config.Acks = Acks.All;
        config.EnableIdempotence = true;
        native = KafkaTopicProtocol.CreateProducerConfig(config);
        Assert.That(native.LingerMs, Is.EqualTo(5));
        Assert.That(native.BatchSize, Is.EqualTo(65536));
        Assert.That(native.Acks, Is.EqualTo(Acks.All));
        Assert.That(native.EnableIdempotence, Is.True);
    }
}
