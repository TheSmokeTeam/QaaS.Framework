using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Protocols.ConfigurationObjects.Kafka;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Framework.Serialization;
using KafkaProducerConfig = Confluent.Kafka.ProducerConfig;
using KafkaConsumerConfig = Confluent.Kafka.ConsumerConfig;


namespace QaaS.Framework.Protocols.Protocols;

public class KafkaTopicProtocol : IReader, ISender, IDisposable
{
    private readonly ILogger _logger;
    private readonly IProducer<byte[]?, byte[]?>? _producer;

    private readonly IConsumer<byte[]?, byte[]?>? _consumer;

    private readonly int? _messageSendMaxRetries;
    private readonly double? _messageSendRetriesIntervalMs;
    private readonly Dictionary<string, object?>? _headers;
    private readonly string _topicName;
    private readonly byte[]? _defaultKafkaKey;
    private readonly int? _partition;

    
    public KafkaTopicProtocol(KafkaTopicReaderConfig configuration, ILogger logger)
    {
        _logger = logger;
        _consumer = new ConsumerBuilder<byte[]?, byte[]?>(new KafkaConsumerConfig
        {
            SaslMechanism = configuration.SaslMechanism,
            SecurityProtocol = configuration.SecurityProtocol,
            SaslUsername = configuration.Username,
            SaslPassword = configuration.Password,
            BootstrapServers = string.Join(", ", configuration.HostNames!),
            GroupId = configuration.GroupId,
            SessionTimeoutMs = configuration.SessionTimeOutMs,
            AutoOffsetReset = configuration.AutoOffsetReset,
            EnableAutoCommit = configuration.EnableAutoCommit,
            HeartbeatIntervalMs = configuration.HeartbeatIntervalMs,
            PartitionAssignmentStrategy = configuration.PartitionAssignmentStrategy,
            MaxPollIntervalMs = configuration.MaxPollIntervalMs,
            FetchMinBytes = configuration.FetchMinBytes,
            FetchWaitMaxMs = configuration.FetchWaitMaxMs
        }).Build();
        _topicName = configuration.TopicName!;
    }

    public KafkaTopicProtocol(KafkaTopicSenderConfig configuration, ILogger logger)
    {
        _logger = logger;
        _producer = new ProducerBuilder<byte[]?, byte[]?>(new KafkaProducerConfig
        {
            SaslMechanism = configuration.SaslMechanism,
            SecurityProtocol = configuration.SecurityProtocol,
            SaslUsername = configuration.Username,
            SaslPassword = configuration.Password,
            BootstrapServers = string.Join(", ", configuration.HostNames!),
            QueueBufferingMaxKbytes = configuration.QueueBufferingMaxKbytes,
            QueueBufferingMaxMessages = configuration.QueueBufferingMaxMessages,
            QueueBufferingBackpressureThreshold = configuration.QueueBufferingBackpressureThreshold,
            CompressionType = configuration.CompressionType,
            CompressionLevel = configuration.CompressionLevel
        }).Build();
        _headers = configuration.Headers;
        _topicName = configuration.TopicName!;
        _messageSendMaxRetries = configuration.MessageSendMaxRetries;
        _messageSendRetriesIntervalMs = configuration.MessageSendRetriesIntervalMs;
        _partition = configuration.Partition;
        _defaultKafkaKey = configuration.DefaultKafkaKey == null
            ? null
            : Encoding.UTF8.GetBytes(configuration.DefaultKafkaKey);
    }

    public SerializationType? GetSerializationType() => null;

    private string GetTopicName(Data<object> data) => data.MetaData?.Kafka?.TopicName ?? _topicName;

    private Headers? GetHeaders(Data<object> data)
    {
        var configHeaders = data.MetaData?.Kafka?.Headers ?? _headers;
        if (configHeaders == null)
            return null;
        var headers = new Headers();
        foreach (var header in configHeaders)
            headers.Add(header.Key, Encoding.UTF8.GetBytes((string)header.Value!));
        return headers;
    }

    public DetailedData<object>? Read(TimeSpan timeoutMs)
    {
        var consumedResult = _consumer!.Consume(timeoutMs);
        if (consumedResult == null) return null;
        _consumer!.Commit(consumedResult);
        return new DetailedData<object>
        {
            Body = consumedResult.Message.Value,
            MetaData = new MetaData
            {
                Kafka = new Kafka
                {
                    MessageKey = consumedResult.Message.Key
                }
            },
            Timestamp = consumedResult.Message.Timestamp.UtcDateTime
        };
    }

    public DetailedData<object> Send(Data<object> dataToSend)
    {
        for (var retry = 1;
             retry <= _messageSendMaxRetries;
             retry++, Thread.Sleep(TimeSpan.FromMilliseconds(_messageSendRetriesIntervalMs!.Value)))
        {
            try
            {
                _producer!.Produce(
                    new TopicPartition(GetTopicName(dataToSend),
                        new Partition(_partition!.Value)),
                    new Message<byte[]?, byte[]?>
                    {
                        Key = dataToSend.MetaData?.Kafka?.MessageKey ?? _defaultKafkaKey,
                        Value = dataToSend.CastObjectData<byte[]>().Body, // Assumes data is byte[]
                        Headers = GetHeaders(dataToSend)
                    }
                );
                break;
            }
            catch (KafkaException produceException) when (retry <= _messageSendMaxRetries - 1)
            {
                _logger.LogWarning("Exception occurred while sending message to KafkaTopic {DestinationName}" +
                                   " - {ProduceException}. Retry {Retry}/{ConfiguredMaxRetries} failed",
                    GetTopicName(dataToSend), produceException, retry, _messageSendMaxRetries);
            }
        }

        return dataToSend.CloneDetailed();
    }

    public void Connect()
    {
        _consumer?.Subscribe(_topicName);
    }

    public void Disconnect()
    {
        _producer?.Flush();
        _consumer?.Unsubscribe();
    }

    public void Dispose()
    {
        _producer?.Dispose();
        _consumer?.Dispose();
    }
}