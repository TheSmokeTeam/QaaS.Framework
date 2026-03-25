using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Kafka;

public record KafkaTopicSenderConfig : BaseKafkaTopicProtocolConfig, ISenderConfig
{
    [Required, Description("Name of the topic to read messages from")]
    public string? TopicName { get; set; }
    
    [Description(
         "The default kafka message key given if no key is given in the generated data under `MetaData` in the kafka key field"),
     DefaultValue(null)]
    public string? DefaultKafkaKey { get; set; } = null;

    [Range(int.MinValue, int.MaxValue), Description(
         "The Kafka partition to produce to, by default -1 is treated as Partition.Any which will mean it uses an unspecified / unknown partition."),
     DefaultValue(-1)]
    public int Partition { get; set; } = -1; // -1 is like Partition.any

    [Description("Max amount of retries when message send to Kafka Topic failed."),
     Range(1, int.MaxValue), DefaultValue(10)]
    public int MessageSendMaxRetries { get; set; } = 10;

    [Description("Time interval in milliseconds to wait between each retry of Kafka Topic message send."),
     Range(0, int.MaxValue), DefaultValue(1000)]
    public int MessageSendRetriesIntervalMs { get; set; } = 1000;

    [Description(
         "Maximum number of messages allowed on the inner producer queue. A value of 0 disables this limit."),
     DefaultValue(100000)]
    public int QueueBufferingMaxMessages { get; set; } = 100000;

    [Description("Maximum total message size sum allowed on the inner producer queue."), DefaultValue(1048576)]
    public int QueueBufferingMaxKbytes { get; set; } = 1048576;

    [Description(
         "The threshold of outstanding not yet transmitted broker requests needed to backpressure the producer's message accumulator."),
     DefaultValue(1)]
    public int QueueBufferingBackpressureThreshold { get; set; } = 1;

    [Description("Default Headers to send messages with, if the message" +
                 " doesn't contain any Headers in its MetaData these Headers are used"), DefaultValue(null)]
    public Dictionary<string, object?>? Headers { get; set; } = null;

    [Description("Compression type to use before sending messages"), DefaultValue(CompressionType.None)]
    public CompressionType CompressionType { get; set; } = CompressionType.None;

    [Description(
         $"Compression level for selected {nameof(CompressionType)} algorithm," +
         $" higher values will result in better compression at the cost of more CPU usage."),
     RangeIfAny(nameof(CompressionType),
         [CompressionType.Gzip, CompressionType.Lz4, CompressionType.Snappy],
         [-1, -1, -1],
         [9, 12, 0])]
    public int CompressionLevel { get; set; } = -1;
    
    [Range(500_000, 4_000_000)]
    [Description("Maximum allowed Kafka message size in bytes. Must not exceed broker/topic limits.")]
    [DefaultValue(1_000_000)]
    public new int MessageMaxBytes { get; set; } = 1_000_000;
}