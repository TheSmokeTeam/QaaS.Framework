using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Kafka;

[PropertyComparison(nameof(HeartbeatIntervalMs), nameof(SessionTimeOutMs), PropertyComparisonOperator.LessThanOrEqual,
    ErrorMessage = $"{nameof(HeartbeatIntervalMs)} must be less than or equal to {nameof(SessionTimeOutMs)}.")]
public record KafkaTopicReaderConfig : BaseKafkaTopicProtocolConfig, IReaderConfig
{
    [Required, Description("Name of the topic to read messages from")]
    public string? TopicName { get; set; }

    [Required, Description("The group name to be used when reading messages")]
    public string? GroupId { get; set; }

    [Description("Where the reader starts reading from in the topic when being created, latest means it starts " +
                 "reading only messages received after it started reading, other options such as Earliest can be found online.")
     , DefaultValue(AutoOffsetReset.Latest)]
    public AutoOffsetReset AutoOffsetReset { get; set; } = AutoOffsetReset.Latest;

    [Range(0, int.MaxValue), Description(
         "The maximum amount of time in milliseconds the reader can go without sending a heartbeat to the reader group coordinator before its considered dead"),
     DefaultValue(9000)]
    public int SessionTimeOutMs { get; set; } = 9000;

    [Description("Whether the read mechanism will commit the offsets automatically and periodically in the background.")
     , DefaultValue(true)]
    public bool EnableAutoCommit { get; set; } = true;

    [Range(0, int.MaxValue), Description("Group session keepalive heartbeat interval"), DefaultValue(1000)]
    public int HeartbeatIntervalMs { get; set; } = 1000;

    [Description("The name of a partition assignment strategy to use. The elected group leader will use a strategy " +
                 "supported by all members of the group to assign partitions to group members. Options: [ " +
                 "`CooperativeSticky` - Ensures that a reader retains its exsting partitions unless it fails," +
                 " providing stability in partition ownership` / " +
                 "`Range` - Distributes partitions evenly across readers by assigning each reader a range of partitions / " +
                 "`RoundRobin` - Assigns partitions to readers in a circular order, distributing them sequentially ] "),
     DefaultValue(PartitionAssignmentStrategy.CooperativeSticky)]
    public PartitionAssignmentStrategy PartitionAssignmentStrategy { get; set; } =
        PartitionAssignmentStrategy.CooperativeSticky;

    [Range(0, int.MaxValue), Description("Maximum allowed time between calls to read messages for high-level" +
                                         " readers. If this interval is exceeded the reader is considered failed " +
                                         "and the group will rebalance in order to reassign the partitions to another" +
                                         " reader group member"), DefaultValue(15000)]
    public int MaxPollIntervalMs { get; set; } = 15000;

    [Range(0, int.MaxValue), Description("Minimum number of bytes the broker responds with. If `FetchWaitMaxMs`" +
                                         " expires the accumulated data will be sent to the client regardless of this setting."),
     DefaultValue(1)]
    public int FetchMinBytes { get; set; } = 1;

    [Range(0, int.MaxValue), Description(
         "Maximum time the broker may wait to fill the Fetch response with `FetchMinBytes` of messages."),
     DefaultValue(2000)]
    public int FetchWaitMaxMs { get; set; } = 2000;
    
    [Range(500_000, 4_000_000)]
    [Description("Maximum allowed Kafka message size in bytes. Must not exceed broker/topic limits.")]
    [DefaultValue(1_000_000)]
    public int MessageMaxBytes { get; set; } = 1_000_000;
}
