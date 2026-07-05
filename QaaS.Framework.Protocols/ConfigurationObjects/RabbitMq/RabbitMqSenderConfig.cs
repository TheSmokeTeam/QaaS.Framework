using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Framework.Protocols.ConfigurationObjects.RabbitMq;

public record RabbitMqSenderConfig : BaseRabbitMqConfig, ISenderConfig
{
    [
        DefaultValue(null),
        MinLength(1),
        RequiredIfAny(nameof(QueueName), null, ""),
        RequiredOrNullBasedOnOtherFieldsConfiguration(new[] { nameof(QueueName) }, false),
        Description(
            "Name of the exchange to send messages to"
                + $"Cannot be set if configured {nameof(QueueName)} to read from."
        )
    ]
    public string? ExchangeName { get; set; } = null;

    [
        Description(
            "Default routing key to send mesages to the exchange with, if the message"
                + " doesn't contain any RoutingKey in its MetaData this routing key is used"
        ),
        DefaultValue("/")
    ]
    public string RoutingKey { get; set; } = "/";

    [
        DefaultValue(null),
        MinLength(1),
        RequiredIfAny(nameof(ExchangeName), null, ""),
        RequiredOrNullBasedOnOtherFieldsConfiguration(new[] { nameof(ExchangeName) }, false),
        Description(
            "Name of the queue to send messages to. "
                + $"Cannot be set if configured {nameof(ExchangeName)} to read from."
        )
    ]
    public string? QueueName { get; set; } = null;

    [
        Description(
            "Default Headers to send messages with, if the message"
                + " doesn't contain any Headers in its MetaData these Headers are used"
        ),
        DefaultValue(null)
    ]
    public Dictionary<string, object?>? Headers { get; set; } = null;

    [
        Description(
            "Default AppId to send messages with, if the message"
                + " doesn't contain AppId in its MetaData this one is Used"
        ),
        DefaultValue(null)
    ]
    public string? AppId { get; set; } = null;

    [
        Description(
            "Default ClusterId to send messages with, if the message"
                + " doesn't contain ClusterId in its MetaData this one is Used"
        ),
        DefaultValue(null)
    ]
    public string? ClusterId { get; set; } = null;

    [
        Description(
            "Default ContentEncoding to send messages with, if the message"
                + " doesn't contain ContentEncoding in its MetaData this one is Used"
        ),
        DefaultValue(null)
    ]
    public string? ContentEncoding { get; set; } = null;

    [
        Description(
            "Default ContentType to send messages with, if the message"
                + " doesn't contain ContentType in its MetaData this one is Used"
        ),
        DefaultValue(null)
    ]
    public string? ContentType { get; set; } = null;

    [
        Description(
            "Default CorrelationId to send messages with, if the message"
                + " doesn't contain CorrelationId in its MetaData this one is Used"
        ),
        DefaultValue(null)
    ]
    public string? CorrelationId { get; set; } = null;

    [
        Range(1, 2),
        Description(
            "Default DeliveryMode to send messages with. Valid values are 1 (transient) and 2 (persistent). "
                + "If the message doesn't contain DeliveryMode in its MetaData this one is Used"
        ),
        DefaultValue(null)
    ]
    public int? DeliveryMode { get; set; } = null;

    [
        Description(
            "Default MessageId to send messages with, if the message"
                + " doesn't contain MessageId in its MetaData this one is Used"
        ),
        DefaultValue(null)
    ]
    public string? MessageId { get; set; } = null;

    [
        Description(
            "Default Persistent flag to send messages with, if the message"
                + " doesn't contain Persistent or DeliveryMode in its MetaData this one is Used. "
                + $"{nameof(DeliveryMode)} takes precedence when both are configured."
        ),
        DefaultValue(null)
    ]
    public bool? Persistent { get; set; } = null;

    [
        Range(0, 255),
        Description(
            "Default Priority to send messages with, if the message"
                + " doesn't contain Priority in its MetaData this one is Used"
        ),
        DefaultValue(null)
    ]
    public int? Priority { get; set; } = null;

    [
        Description(
            "Default ReplyTo to send messages with, if the message"
                + " doesn't contain ReplyTo in its MetaData this one is Used"
        ),
        DefaultValue(null)
    ]
    public string? ReplyTo { get; set; } = null;

    [
        Range(typeof(long), "0", "9223372036854775807"),
        Description(
            "Default RabbitMQ timestamp as Unix time seconds, if the message"
                + " doesn't contain TimestampUnixTime in its MetaData this one is Used"
        ),
        DefaultValue(null)
    ]
    public long? TimestampUnixTime { get; set; } = null;

    [
        Description(
            "Default Type to send messages with, if the message"
                + " doesn't contain Type in its MetaData this one is Used"
        ),
        DefaultValue(null)
    ]
    public string? Type { get; set; } = null;

    [
        Description(
            "Default UserId to send messages with, if the message"
                + " doesn't contain UserId in its MetaData this one is Used"
        ),
        DefaultValue(null)
    ]
    public string? UserId { get; set; } = null;

    [
        Description(
            "Default Message expiration duration to send messages with, if the message"
                + " doesn't contain any Expiration in its MetaData this Expiration is used"
        ),
        DefaultValue(null)
    ]
    public string? Expiration { get; set; } = null;
};
