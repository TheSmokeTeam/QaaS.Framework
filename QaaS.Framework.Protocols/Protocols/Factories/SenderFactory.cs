using QaaS.Framework.Protocols.ConfigurationObjects;
using QaaS.Framework.Protocols.ConfigurationObjects.Elastic;
using QaaS.Framework.Protocols.ConfigurationObjects.Kafka;
using QaaS.Framework.Protocols.ConfigurationObjects.MongoDb;
using QaaS.Framework.Protocols.ConfigurationObjects.RabbitMq;
using QaaS.Framework.Protocols.ConfigurationObjects.Redis;
using QaaS.Framework.Protocols.ConfigurationObjects.S3;
using QaaS.Framework.Protocols.ConfigurationObjects.Sftp;
using QaaS.Framework.Protocols.ConfigurationObjects.Socket;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.Session;

namespace QaaS.Framework.Protocols.Protocols.Factories;

/// <summary>
/// Factory class responsible for creating sender instances based on configuration types.
/// Provides methods to create both chunkable senders and singular senders.
/// </summary>
public static class SenderFactory
{
    /// <summary>
    /// Creates either a chunkable sender or a singular sender instance based on the provided configuration type.
    /// </summary>
    /// <param name="isChunkable">Determines rather the sender should be singular or chunkable</param>
    /// <param name="type">The sender configuration that determines the type of sender to create</param>
    /// <param name="logger">Logger instance for logging operations and errors</param>
    /// <param name="dataFilter">Optional data filter to apply to the sender's data processing</param>
    /// <returns>A tuple containing an ISender (nullable) and an IChunkSender (nullable) configured according to the provided configuration.
    /// The first item will be non-null for singular senders, the second for chunkable senders.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the provided configuration type is not supported or recognized</exception>
    public static (ISender?, IChunkSender?) CreateSender(bool isChunkable, ISenderConfig type, ILogger logger, DataFilter? dataFilter)
    {
       IConnectable connectable = type switch
       {
           // Singular senders
           RedisSenderConfig config => new RedisProtocol(config, logger),
           MsSqlSenderConfig config => new MsSqlProtocol(config, logger),
           OracleSenderConfig config => new OracleSqlProtocol(config, logger),
           MongoDbCollectionSenderConfig config => new MongoDbProtocol(config, logger),
           ElasticSenderConfig config => new ElasticProtocol(config, dataFilter!, logger),
           
           // Chunkable senders
           RabbitMqSenderConfig config => new RabbitMqProtocol(config, logger),
           KafkaTopicSenderConfig config => new KafkaTopicProtocol(config, logger),
           SftpSenderConfig config => new SftpProtocol(config, logger),
           SocketSenderConfig config => new SocketProtocol(config, logger),
           S3BucketSenderConfig config => new S3Protocol(config, logger),
           
           // Senders which support both
           PostgreSqlSenderConfig config => new PostgreSqlProtocol(config, logger),
           
           _ => throw new InvalidOperationException($"Protocol type {type.GetType().Name} is not supported")
       };

       return isChunkable ? (null, (IChunkSender)connectable) : ((ISender)connectable, null);
    }
}
