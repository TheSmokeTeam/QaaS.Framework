using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;
using QaaS.Framework.Protocols.ConfigurationObjects;
using QaaS.Framework.Protocols.ConfigurationObjects.Elastic;
using QaaS.Framework.Protocols.ConfigurationObjects.Grpc;
using QaaS.Framework.Protocols.ConfigurationObjects.Kafka;
using QaaS.Framework.Protocols.ConfigurationObjects.RabbitMq;
using QaaS.Framework.Protocols.ConfigurationObjects.Sftp;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class ProtocolConfigurationObjectsTests
{
    [Test]
    public void KafkaConfigurationObjects_DefaultValuesAndValidation_Work()
    {
        var baseConfig = new BaseKafkaTopicProtocolConfig
        {
            HostNames = ["host1:9092"],
            Username = "user",
            Password = "pass"
        };
        var reader = new KafkaTopicReaderConfig
        {
            HostNames = ["host1:9092"],
            Username = "user",
            Password = "pass",
            TopicName = "topic",
            GroupId = "group"
        };
        var sender = new KafkaTopicSenderConfig
        {
            HostNames = ["host1:9092"],
            Username = "user",
            Password = "pass",
            TopicName = "topic",
            CompressionType = CompressionType.Gzip,
            CompressionLevel = 3
        };
        var invalidSender = sender with { CompressionLevel = 99 };

        Assert.Multiple(() =>
        {
            Assert.That(baseConfig.SecurityProtocol, Is.EqualTo(SecurityProtocol.SaslPlaintext));
            Assert.That(baseConfig.SaslMechanism, Is.EqualTo(SaslMechanism.ScramSha256));
            Assert.That(reader.AutoOffsetReset, Is.EqualTo(AutoOffsetReset.Latest));
            Assert.That(reader.EnableAutoCommit, Is.True);
            Assert.That(sender.QueueBufferingMaxMessages, Is.EqualTo(100000));
            Assert.That(sender.QueueBufferingBackpressureThreshold, Is.EqualTo(1));
            Assert.That(Validator.TryValidateObject(sender, new ValidationContext(sender), null, true), Is.True);
            Assert.That(Validator.TryValidateObject(invalidSender, new ValidationContext(invalidSender), null, true), Is.False);
        });
    }

    [Test]
    public void RabbitMqConfigurationObjects_DefaultValues_Work()
    {
        var baseConfig = new BaseRabbitMqConfig { Host = "localhost" };
        var sender = new RabbitMqSenderConfig
        {
            Host = "localhost",
            QueueName = "q"
        };
        var reader = new RabbitMqReaderConfig
        {
            Host = "localhost",
            QueueName = "q"
        };

        Assert.Multiple(() =>
        {
            Assert.That(baseConfig.Username, Is.EqualTo("admin"));
            Assert.That(baseConfig.Password, Is.EqualTo("admin"));
            Assert.That(baseConfig.Port, Is.EqualTo(5672));
            Assert.That(sender.RoutingKey, Is.EqualTo("/"));
            Assert.That(sender.ExchangeName, Is.Null);
            Assert.That(reader.CreatedQueueTimeToExpireMs, Is.EqualTo(300000));
            Assert.That(Validator.TryValidateObject(sender, new ValidationContext(sender), null, true), Is.True);
            Assert.That(Validator.TryValidateObject(reader, new ValidationContext(reader), null, true), Is.True);
        });
    }

    [Test]
    public void ElasticConfigurationObjects_DefaultValues_Work()
    {
        var baseConfig = new BaseElasticConfig
        {
            Url = "http://localhost:9200",
            Username = "elastic",
            Password = "secret"
        };
        var indices = new BaseElasticIndices
        {
            Url = "http://localhost:9200",
            Username = "elastic",
            Password = "secret",
            IndexPattern = "logs-*"
        };
        var regex = new ElasticIndicesRegex
        {
            Url = "http://localhost:9200",
            Username = "elastic",
            Password = "secret",
            IndexPattern = "logs-*"
        };
        var reader = new ElasticReaderConfig
        {
            Url = "http://localhost:9200",
            Username = "elastic",
            Password = "secret",
            IndexPattern = "logs-*"
        };
        var sender = new ElasticSenderConfig
        {
            Url = "http://localhost:9200",
            Username = "elastic",
            Password = "secret",
            IndexName = "logs-2026"
        };

        Assert.Multiple(() =>
        {
            Assert.That(baseConfig.RequestTimeoutMs, Is.EqualTo(30000));
            Assert.That(indices.IndexPattern, Is.EqualTo("logs-*"));
            Assert.That(regex.MatchQueryString, Is.EqualTo("*"));
            Assert.That(reader.TimestampField, Is.EqualTo("@timestamp"));
            Assert.That(reader.ReadBatchSize, Is.EqualTo(1000));
            Assert.That(sender.PublishAsync, Is.False);
            Assert.That(Validator.TryValidateObject(sender, new ValidationContext(sender), null, true), Is.True);
        });
    }

    [Test]
    public void GrpcAndSftpConfigurationObjects_DefaultValues_Work()
    {
        var grpc = new GrpcTransactorConfig
        {
            Host = "localhost",
            Port = 5001,
            AssemblyName = "Asm",
            ProtoNameSpace = "Ns",
            ServiceName = "Svc",
            RpcName = "Call"
        };

        var sftpBase = new BaseSftpConfig
        {
            Hostname = "host",
            Username = "user",
            Password = "pass",
            Path = "/tmp"
        };

        var sftpSender = new SftpSenderConfig
        {
            Hostname = "host",
            Username = "user",
            Password = "pass",
            Path = "/tmp",
            Prefix = "pref-"
        };

        Assert.Multiple(() =>
        {
            Assert.That(grpc.Port, Is.EqualTo((ushort)5001));
            Assert.That(Validator.TryValidateObject(grpc, new ValidationContext(grpc), null, true), Is.True);
            Assert.That(sftpBase.Port, Is.EqualTo(22));
            Assert.That(sftpSender.Prefix, Is.EqualTo("pref-"));
            Assert.That(sftpSender.NamingType, Is.EqualTo(ObjectNamingGeneratorType.GrowingNumericalSeries));
            Assert.That(Validator.TryValidateObject(sftpSender, new ValidationContext(sftpSender), null, true), Is.True);
        });
    }
}
