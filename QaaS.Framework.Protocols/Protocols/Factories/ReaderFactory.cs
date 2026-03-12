using QaaS.Framework.Protocols.ConfigurationObjects;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Protocols.ConfigurationObjects.Elastic;
using QaaS.Framework.Protocols.ConfigurationObjects.IbmMq;
using QaaS.Framework.Protocols.ConfigurationObjects.Kafka;
using QaaS.Framework.Protocols.ConfigurationObjects.RabbitMq;
using QaaS.Framework.Protocols.ConfigurationObjects.Redis;
using QaaS.Framework.Protocols.ConfigurationObjects.S3;
using QaaS.Framework.Protocols.ConfigurationObjects.Socket;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.SDK.Session;

namespace QaaS.Framework.Protocols.Protocols.Factories;

/// <summary>
/// Factory class responsible for creating reader instances based on configuration types.
/// Provides methods to create both chunkable readers and singular readers.
/// </summary>
public static class ReaderFactory
{
    /// <summary>
    /// Creates either a chunkable reader or a singular reader instance based on the provided configuration type.
    /// </summary>
    /// <param name="type">The reader configuration that determines the type of reader to create</param>
    /// <param name="logger">Logger instance for logging operations and errors</param>
    /// <param name="dataFilter">Optional data filter to apply to the reader's data processing</param>
    /// <returns>A tuple containing an IReader (nullable) and an IChunkReader (nullable) configured according to the provided configuration.
    /// The first item will be non-null for singular readers, the second for chunkable readers.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the provided configuration type is not supported or recognized</exception>
    public static (IReader?, IChunkReader?) CreateReader(IReaderConfig type, ILogger logger, DataFilter? dataFilter)
    {
        return type switch
        {
            // Singular readers
            RabbitMqReaderConfig config => (new RabbitMqProtocol(config, logger), null),
            KafkaTopicReaderConfig config => (new KafkaTopicProtocol(config, logger), null),
            SocketReaderConfig config => (new SocketProtocol(config, logger), null),
            IbmMqReaderConfig config => (new IbmMqProtocol(config), null),
            RedisReaderConfig config => (new RedisReaderProtocol(config, logger), null),
            
            // Chunkable readers
            PostgreSqlReaderConfig config => (null, new PostgreSqlProtocol(config, logger)),
            OracleReaderConfig config => (null, new OracleSqlProtocol(config, logger)),
            MsSqlReaderConfig config => (null, new MsSqlProtocol(config, logger)),
            TrinoReaderConfig config => (null, new TrinoSqlProtocol(config, logger)),
            ElasticReaderConfig config => (null, new ElasticProtocol(config, dataFilter!, logger)),
            S3BucketReaderConfig config => (null, new S3Protocol(config, dataFilter!, logger)),
            
            _ => throw new InvalidOperationException($"Protocol type {type.GetType().Name} is not supported")
        };
    }
}
