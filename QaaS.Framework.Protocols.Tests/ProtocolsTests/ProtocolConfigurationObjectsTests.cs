using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;
using QaaS.Framework.Protocols.ConfigurationObjects;
using QaaS.Framework.Protocols.ConfigurationObjects.Elastic;
using QaaS.Framework.Protocols.ConfigurationObjects.Grpc;
using QaaS.Framework.Protocols.ConfigurationObjects.Http;
using QaaS.Framework.Protocols.ConfigurationObjects.Kafka;
using QaaS.Framework.Protocols.ConfigurationObjects.RabbitMq;
using QaaS.Framework.Protocols.ConfigurationObjects.Redis;
using QaaS.Framework.Protocols.ConfigurationObjects.S3;
using QaaS.Framework.Protocols.ConfigurationObjects.Sftp;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class ProtocolConfigurationObjectsTests
{
    private static (bool IsValid, List<ValidationResult> Results) Validate(object value)
    {
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(
            value,
            new ValidationContext(value),
            results,
            true
        );
        return (isValid, results);
    }

    [Test]
    public void KafkaConfigurationObjects_DefaultValuesAndValidation_Work()
    {
        var baseConfig = new BaseKafkaTopicProtocolConfig
        {
            HostNames = ["host1:9092"],
            Username = "user",
            Password = "pass",
        };
        var reader = new KafkaTopicReaderConfig
        {
            HostNames = ["host1:9092"],
            Username = "user",
            Password = "pass",
            TopicName = "topic",
            GroupId = "group",
        };
        var invalidReader = reader with { HeartbeatIntervalMs = reader.SessionTimeOutMs + 1 };
        var sender = new KafkaTopicSenderConfig
        {
            HostNames = ["host1:9092"],
            Username = "user",
            Password = "pass",
            TopicName = "topic",
            CompressionType = CompressionType.Gzip,
            CompressionLevel = 3,
        };
        var invalidSender = sender with { CompressionLevel = 99 };
        var duplicateHosts = baseConfig with { HostNames = ["host1:9092", "host1:9092"] };
        var missingHosts = baseConfig with { HostNames = [] };

        Assert.Multiple(() =>
        {
            Assert.That(baseConfig.SecurityProtocol, Is.EqualTo(SecurityProtocol.SaslPlaintext));
            Assert.That(baseConfig.SaslMechanism, Is.EqualTo(SaslMechanism.ScramSha256));
            Assert.That(reader.AutoOffsetReset, Is.EqualTo(AutoOffsetReset.Latest));
            Assert.That(reader.EnableAutoCommit, Is.True);
            Assert.That(sender.QueueBufferingMaxMessages, Is.EqualTo(100000));
            Assert.That(sender.QueueBufferingBackpressureThreshold, Is.EqualTo(1));
            Assert.That(sender.MessageMaxBytes, Is.EqualTo(1_000_000));
            Assert.That(reader.MessageMaxBytes, Is.EqualTo(1_000_000));
            Assert.That(Validate(sender).IsValid, Is.True);
            Assert.That(Validate(invalidSender).IsValid, Is.False);
            Assert.That(Validate(reader).IsValid, Is.True);
            Assert.That(Validate(invalidReader).IsValid, Is.False);
            Assert.That(Validate(duplicateHosts).IsValid, Is.False);
            Assert.That(Validate(missingHosts).IsValid, Is.False);
        });
    }

    [Test]
    public void KafkaSenderConfig_MessageMaxBytes_HonorsLibrdkafkaBounds()
    {
        // librdkafka `message.max.bytes` valid range is 1000 .. 1000000000 (Confluent.Kafka CONFIGURATION.md).
        KafkaTopicSenderConfig WithSize(int size) =>
            new()
            {
                HostNames = ["host1:9092"],
                Username = "user",
                Password = "pass",
                TopicName = "topic",
                MessageMaxBytes = size,
            };

        Assert.Multiple(() =>
        {
            // Lowest and highest values accepted by the Confluent library are valid.
            Assert.That(
                Validate(WithSize(1_000)).IsValid,
                Is.True,
                "lower bound 1000 should be valid"
            );
            Assert.That(
                Validate(WithSize(1_000_000_000)).IsValid,
                Is.True,
                "upper bound 1000000000 should be valid"
            );
            // Just outside the library bounds must be rejected by validation.
            Assert.That(
                Validate(WithSize(999)).IsValid,
                Is.False,
                "below lower bound must be invalid"
            );
            Assert.That(
                Validate(WithSize(1_000_000_001)).IsValid,
                Is.False,
                "above upper bound must be invalid"
            );
        });
    }

    [Test]
    public void S3BucketReaderConfig_DefaultsKeepLegacyReadBehavior()
    {
        var reader = new S3BucketReaderConfig();

        Assert.Multiple(() =>
        {
            Assert.That(reader.ReadFromRunStartTime, Is.False);
            Assert.That(reader.ReadStorageHeaders, Is.False);
            Assert.That(reader.SkipEmptyObjects, Is.False);
            Assert.That(reader.Prefix, Is.Empty);
            Assert.That(reader.Delimiter, Is.Empty);
            Assert.That(reader.MaximumRetryCount, Is.Null);
        });
    }

    [Test]
    public void RabbitMqConfigurationObjects_DefaultValues_AndMutualExclusionRules_Work()
    {
        var baseConfig = new BaseRabbitMqConfig { Host = "localhost" };
        var sender = new RabbitMqSenderConfig { Host = "localhost", QueueName = "q" };
        var reader = new RabbitMqReaderConfig { Host = "localhost", QueueName = "q" };
        var invalidSenderBothTargets = sender with { ExchangeName = "exchange" };
        var invalidReaderMissingTarget = new RabbitMqReaderConfig { Host = "localhost" };
        var invalidSenderEmptyQueue = new RabbitMqSenderConfig
        {
            Host = "localhost",
            QueueName = string.Empty,
        };
        var invalidSenderDeliveryMode = sender with { DeliveryMode = 3 };
        var invalidSenderPriority = sender with { Priority = 256 };
        var invalidSenderTimestamp = sender with { TimestampUnixTime = -1 };

        Assert.Multiple(() =>
        {
            Assert.That(baseConfig.Username, Is.EqualTo("admin"));
            Assert.That(baseConfig.Password, Is.EqualTo("admin"));
            Assert.That(baseConfig.Port, Is.EqualTo(5672));
            Assert.That(baseConfig.Durable, Is.False);
            Assert.That(baseConfig.Exclusive, Is.False);
            Assert.That(baseConfig.AutoDelete, Is.False);
            Assert.That(baseConfig.Arguments, Is.Null);
            Assert.That(sender.RoutingKey, Is.EqualTo("/"));
            Assert.That(sender.ExchangeName, Is.Null);
            Assert.That(sender.Durable, Is.False);
            Assert.That(sender.Exclusive, Is.False);
            Assert.That(sender.AutoDelete, Is.False);
            Assert.That(sender.Arguments, Is.Null);
            Assert.That(sender.AppId, Is.Null);
            Assert.That(sender.ClusterId, Is.Null);
            Assert.That(sender.ContentEncoding, Is.Null);
            Assert.That(sender.ContentType, Is.Null);
            Assert.That(sender.CorrelationId, Is.Null);
            Assert.That(sender.DeliveryMode, Is.Null);
            Assert.That(sender.Expiration, Is.Null);
            Assert.That(sender.Headers, Is.Null);
            Assert.That(sender.MessageId, Is.Null);
            Assert.That(sender.Persistent, Is.Null);
            Assert.That(sender.Priority, Is.Null);
            Assert.That(sender.ReplyTo, Is.Null);
            Assert.That(sender.TimestampUnixTime, Is.Null);
            Assert.That(sender.Type, Is.Null);
            Assert.That(sender.UserId, Is.Null);
            Assert.That(reader.CreatedQueueTimeToExpireMs, Is.EqualTo(300000));
            Assert.That(reader.Durable, Is.False);
            Assert.That(reader.Exclusive, Is.False);
            Assert.That(reader.AutoDelete, Is.False);
            Assert.That(reader.Arguments, Is.Null);
            var customReader = reader with
            {
                Durable = true,
                Exclusive = true,
                AutoDelete = true,
                Arguments = new Dictionary<string, object?> { ["x-message-ttl"] = 5000 }
            };
            Assert.That(Validate(customReader).IsValid, Is.True);
            Assert.That(customReader.Durable, Is.True);
            Assert.That(customReader.Exclusive, Is.True);
            Assert.That(customReader.AutoDelete, Is.True);
            Assert.That(customReader.Arguments?["x-message-ttl"], Is.EqualTo(5000));
            Assert.That(Validate(sender).IsValid, Is.True);
            Assert.That(Validate(reader).IsValid, Is.True);
            Assert.That(Validate(invalidSenderBothTargets).IsValid, Is.False);
            Assert.That(Validate(invalidReaderMissingTarget).IsValid, Is.False);
            Assert.That(Validate(invalidSenderEmptyQueue).IsValid, Is.False);
            Assert.That(Validate(invalidSenderDeliveryMode).IsValid, Is.False);
            Assert.That(Validate(invalidSenderPriority).IsValid, Is.False);
            Assert.That(Validate(invalidSenderTimestamp).IsValid, Is.False);
        });
    }

    [Test]
    public void ElasticConfigurationObjects_DefaultValues_Work()
    {
        var baseConfig = new BaseElasticConfig
        {
            Url = "http://localhost:9200",
            Username = "elastic",
            Password = "secret",
        };
        var indices = new BaseElasticIndices
        {
            Url = "http://localhost:9200",
            Username = "elastic",
            Password = "secret",
            IndexPattern = "logs-*",
        };
        var regex = new ElasticIndicesRegex
        {
            Url = "http://localhost:9200",
            Username = "elastic",
            Password = "secret",
            IndexPattern = "logs-*",
        };
        var reader = new ElasticReaderConfig
        {
            Url = "http://localhost:9200",
            Username = "elastic",
            Password = "secret",
            IndexPattern = "logs-*",
        };
        var sender = new ElasticSenderConfig
        {
            Url = "http://localhost:9200",
            Username = "elastic",
            Password = "secret",
            IndexName = "logs-2026",
        };

        Assert.Multiple(() =>
        {
            Assert.That(baseConfig.RequestTimeoutMs, Is.EqualTo(30000));
            Assert.That(indices.IndexPattern, Is.EqualTo("logs-*"));
            Assert.That(regex.MatchQueryString, Is.EqualTo("*"));
            Assert.That(reader.TimestampField, Is.EqualTo("@timestamp"));
            Assert.That(reader.ReadBatchSize, Is.EqualTo(1000));
            Assert.That(sender.PublishAsync, Is.False);
            Assert.That(Validate(sender).IsValid, Is.True);
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
            RpcName = "Call",
        };

        var sftpBase = new BaseSftpConfig
        {
            Hostname = "host",
            Username = "user",
            Password = "pass",
            Path = "/tmp",
        };

        var sftpSender = new SftpSenderConfig
        {
            Hostname = "host",
            Username = "user",
            Password = "pass",
            Path = "/tmp",
            Prefix = "pref-",
        };

        Assert.Multiple(() =>
        {
            Assert.That(grpc.Port, Is.EqualTo((ushort)5001));
            Assert.That(Validate(grpc).IsValid, Is.True);
            Assert.That(sftpBase.Port, Is.EqualTo(22));
            Assert.That(sftpSender.Prefix, Is.EqualTo("pref-"));
            Assert.That(
                sftpSender.NamingType,
                Is.EqualTo(ObjectNamingGeneratorType.GrowingNumericalSeries)
            );
            Assert.That(Validate(sftpSender).IsValid, Is.True);
        });
    }

    [Test]
    public void RedisAndJwtConfigurationObjects_ValidateConditionalRules()
    {
        var validRedisReader = new RedisReaderConfig
        {
            HostNames = ["localhost:6379"],
            RedisDataType = RedisDataType.HashSet,
            Key = "orders",
            HashField = "id",
        };
        var invalidRedisReader = validRedisReader with { HashField = null };

        var validClaimsJwt = new JwtAuthConfig
        {
            Secret = "secret",
            Claims = new Dictionary<string, string> { ["sub"] = "1" },
        };
        var validHierarchicalJwt = new JwtAuthConfig
        {
            Secret = "secret",
            HierarchicalClaims = "sub: 1",
        };
        var invalidJwtBothSources = new JwtAuthConfig
        {
            Secret = "secret",
            Claims = new Dictionary<string, string> { ["sub"] = "1" },
            HierarchicalClaims = "sub: 1",
        };
        var invalidJwtYaml = new JwtAuthConfig { Secret = "secret", HierarchicalClaims = "[bad" };

        Assert.Multiple(() =>
        {
            Assert.That(Validate(validRedisReader).IsValid, Is.True);
            Assert.That(Validate(invalidRedisReader).IsValid, Is.False);
            Assert.That(Validate(validClaimsJwt).IsValid, Is.True);
            Assert.That(Validate(validHierarchicalJwt).IsValid, Is.True);
            Assert.That(Validate(invalidJwtBothSources).IsValid, Is.False);
            Assert.That(Validate(invalidJwtYaml).IsValid, Is.False);
        });
    }

    [Test]
    public void SqlConfigurationObjects_RejectDuplicateIgnoredColumns()
    {
        var validReader = new SqlReaderConfig
        {
            TableName = "tbl",
            ConnectionString = "Server=localhost;Database=db;User Id=u;Password=p;",
            ColumnsToIgnore = ["created_at", "updated_at"],
        };
        var invalidReader = validReader with { ColumnsToIgnore = ["created_at", "created_at"] };

        Assert.Multiple(() =>
        {
            Assert.That(Validate(validReader).IsValid, Is.True);
            Assert.That(Validate(invalidReader).IsValid, Is.False);
        });
    }

    [Test]
    public void SqlUdtSenderConfig_Defaults_Work()
    {
        var config = new SqlUdtSenderConfig
        {
            TableName = "tbl",
            ConnectionString = "Server=localhost;Database=db;User Id=u;Password=p;",
        };

        Assert.That(config.IsUDTInsertion, Is.False);
    }
}
