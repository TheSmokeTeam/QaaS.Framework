using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Protocols.ConfigurationObjects.Kafka;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Framework.Serialization;
using KafkaConsumerConfig = Confluent.Kafka.ConsumerConfig;
using KafkaProducerConfig = Confluent.Kafka.ProducerConfig;

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

    private readonly KafkaCommitMode _commitMode;
    private readonly int _commitBatchSize;
    private readonly Dictionary<TopicPartition, Offset> _pendingOffsets = new();
    private int _pendingReceipts;
    private readonly int? _flushTimeoutMs;
    private readonly bool _reportDeliveryErrors;
    private KafkaException? _deliveryError;

    public KafkaTopicProtocol(KafkaTopicReaderConfig configuration, ILogger logger)
        : this(configuration, logger, null) { }

    internal KafkaTopicProtocol(
        KafkaTopicReaderConfig configuration,
        ILogger logger,
        IConsumer<byte[]?, byte[]?>? consumer
    )
    {
        if (!Enum.IsDefined(configuration.CommitMode))
            throw new ArgumentOutOfRangeException(nameof(configuration.CommitMode));
        if (configuration.CommitMode == KafkaCommitMode.Batched)
            ArgumentOutOfRangeException.ThrowIfLessThan(configuration.CommitBatchSize, 1);
        _commitMode = configuration.CommitMode;
        _commitBatchSize = configuration.CommitBatchSize;
        _logger = logger;
        _topicName = configuration.TopicName!;
        if (consumer != null)
        {
            _consumer = consumer;
            return;
        }
        var builder = new ConsumerBuilder<byte[]?, byte[]?>(CreateConsumerConfig(configuration));
        if (_commitMode == KafkaCommitMode.Batched)
        {
            builder.SetPartitionsRevokedHandler(
                (client, partitions) =>
                {
                    var revoked = partitions.Select(x => x.TopicPartition).ToArray();
                    try
                    {
                        CommitPending(client, revoked);
                    }
                    finally
                    {
                        ForgetPartitions(revoked);
                    }
                }
            );
            builder.SetPartitionsLostHandler(
                (_, partitions) => ForgetPartitions(partitions.Select(x => x.TopicPartition))
            );
        }
        _consumer = builder.Build();
    }

    internal static KafkaConsumerConfig CreateConsumerConfig(
        KafkaTopicReaderConfig configuration
    ) =>
        new()
        {
            SaslMechanism = configuration.SaslMechanism,
            SecurityProtocol = configuration.SecurityProtocol,
            SaslUsername = configuration.Username,
            SaslPassword = configuration.Password,
            BootstrapServers = string.Join(", ", configuration.HostNames!),
            GroupId = configuration.GroupId,
            SessionTimeoutMs = configuration.SessionTimeOutMs,
            AutoOffsetReset = configuration.AutoOffsetReset,
            EnableAutoCommit =
                configuration.CommitMode == KafkaCommitMode.PerMessage
                && configuration.EnableAutoCommit,
            EnableAutoOffsetStore =
                configuration.CommitMode == KafkaCommitMode.Batched ? false : null,
            HeartbeatIntervalMs = configuration.HeartbeatIntervalMs,
            PartitionAssignmentStrategy = configuration.PartitionAssignmentStrategy,
            MaxPollIntervalMs = configuration.MaxPollIntervalMs,
            FetchMinBytes = configuration.FetchMinBytes,
            FetchWaitMaxMs = configuration.FetchWaitMaxMs,
            MessageMaxBytes = configuration.MessageMaxBytes,
        };

    internal static KafkaProducerConfig CreateProducerConfig(
        KafkaTopicSenderConfig configuration
    ) =>
        new()
        {
            LingerMs = configuration.LingerMs,
            BatchSize = configuration.BatchSize,
            Acks = configuration.Acks,
            EnableIdempotence = configuration.EnableIdempotence,
            MessageTimeoutMs = configuration.MessageTimeoutMs,
            MaxInFlight = configuration.MaxInFlight,
            SaslMechanism = configuration.SaslMechanism,
            SecurityProtocol = configuration.SecurityProtocol,
            SaslUsername = configuration.Username,
            SaslPassword = configuration.Password,
            BootstrapServers = string.Join(", ", configuration.HostNames!),
            QueueBufferingMaxKbytes = configuration.QueueBufferingMaxKbytes,
            QueueBufferingMaxMessages = configuration.QueueBufferingMaxMessages,
            QueueBufferingBackpressureThreshold = configuration.QueueBufferingBackpressureThreshold,
            CompressionType = configuration.CompressionType,
            CompressionLevel = configuration.CompressionLevel,
            MessageMaxBytes = configuration.MessageMaxBytes,
        };

    public KafkaTopicProtocol(KafkaTopicSenderConfig configuration, ILogger logger)
        : this(configuration, logger, null) { }

    internal KafkaTopicProtocol(
        KafkaTopicSenderConfig configuration,
        ILogger logger,
        IProducer<byte[]?, byte[]?>? producer
    )
    {
        _logger = logger;
        _producer =
            producer
            ?? new ProducerBuilder<byte[]?, byte[]?>(CreateProducerConfig(configuration)).Build();
        _flushTimeoutMs = configuration.FlushTimeoutMs;
        _reportDeliveryErrors = configuration.ReportDeliveryErrors;
        _headers = configuration.Headers;
        _topicName = configuration.TopicName!;
        _messageSendMaxRetries = configuration.MessageSendMaxRetries;
        _messageSendRetriesIntervalMs = configuration.MessageSendRetriesIntervalMs;
        _partition = configuration.Partition;
        _defaultKafkaKey =
            configuration.DefaultKafkaKey == null
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
        if (consumedResult == null)
        {
            if (_commitMode == KafkaCommitMode.Batched)
                CommitPending(_consumer);
            return null;
        }
        var data = new DetailedData<object>
        {
            Body = consumedResult.Message.Value,
            MetaData = new MetaData
            {
                Kafka = new Kafka
                {
                    MessageKey = consumedResult.Message.Key,
                    Headers = consumedResult
                        .Message.Headers?.GroupBy(header => header.Key)
                        .ToDictionary(
                            group => group.Key,
                            object? (group) => Encoding.UTF8.GetString(group.Last().GetValueBytes())
                        ),
                },
            },
            Timestamp = consumedResult.Message.Timestamp.UtcDateTime,
        };
        if (_commitMode == KafkaCommitMode.PerMessage)
            _consumer.Commit(consumedResult);
        else
        {
            _pendingOffsets[consumedResult.TopicPartition] = consumedResult.Offset + 1;
            if (++_pendingReceipts >= _commitBatchSize)
                CommitPending(_consumer);
        }
        return data;
    }

    internal void CommitPending(
        IConsumer<byte[]?, byte[]?> client,
        IEnumerable<TopicPartition>? partitions = null
    )
    {
        var selected = partitions?.ToHashSet();
        var offsets = _pendingOffsets
            .Where(item => selected == null || selected.Contains(item.Key))
            .Select(item => new TopicPartitionOffset(item.Key, item.Value))
            .ToArray();
        if (offsets.Length == 0)
            return;
        client.Commit(offsets); // Keep pending offsets if the commit fails.
        foreach (var offset in offsets)
            _pendingOffsets.Remove(offset.TopicPartition);
        if (_pendingOffsets.Count == 0)
            _pendingReceipts = 0;
    }

    internal void ForgetPartitions(IEnumerable<TopicPartition> partitions)
    {
        foreach (var partition in partitions)
            _pendingOffsets.Remove(partition);
        if (_pendingOffsets.Count == 0)
            _pendingReceipts = 0;
    }

    private void ThrowDeliveryError()
    {
        var error = Volatile.Read(ref _deliveryError);
        if (error != null)
            throw error;
    }

    private void OnDelivery(DeliveryReport<byte[]?, byte[]?> report)
    {
        if (report.Error.IsError)
            Interlocked.CompareExchange(ref _deliveryError, new KafkaException(report.Error), null);
    }

    public DetailedData<object> Send(Data<object> dataToSend)
    {
        ThrowDeliveryError();
        for (
            var retry = 1;
            retry <= _messageSendMaxRetries;
            retry++, Thread.Sleep(TimeSpan.FromMilliseconds(_messageSendRetriesIntervalMs!.Value))
        )
        {
            try
            {
                _producer!.Produce(
                    new TopicPartition(GetTopicName(dataToSend), new Partition(_partition!.Value)),
                    new Message<byte[]?, byte[]?>
                    {
                        Key = dataToSend.MetaData?.Kafka?.MessageKey ?? _defaultKafkaKey,
                        Value = dataToSend.CastObjectData<byte[]>().Body, // Assumes data is byte[]
                        Headers = GetHeaders(dataToSend),
                    },
                    _reportDeliveryErrors ? OnDelivery : null
                );
                break;
            }
            catch (KafkaException produceException) when (retry <= _messageSendMaxRetries - 1)
            {
                _logger.LogWarning(
                    "Exception occurred while sending message to KafkaTopic {DestinationName}"
                        + " - {ProduceException}. Retry {Retry}/{ConfiguredMaxRetries} failed",
                    GetTopicName(dataToSend),
                    produceException,
                    retry,
                    _messageSendMaxRetries
                );
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
        if (_producer != null)
        {
            if (_flushTimeoutMs.HasValue)
            {
                var remaining = _producer.Flush(TimeSpan.FromMilliseconds(_flushTimeoutMs.Value));
                if (remaining != 0)
                    throw new TimeoutException(
                        $"Kafka flush deadline expired with {remaining} outstanding records."
                    );
            }
            else
                _producer.Flush();
            ThrowDeliveryError();
        }
        if (_consumer != null && _commitMode == KafkaCommitMode.Batched)
        {
            try
            {
                CommitPending(_consumer);
            }
            finally
            {
                _consumer.Close();
            }
        }
        else
            _consumer?.Unsubscribe();
    }

    public void Dispose()
    {
        _producer?.Dispose();
        _consumer?.Dispose();
    }
}
