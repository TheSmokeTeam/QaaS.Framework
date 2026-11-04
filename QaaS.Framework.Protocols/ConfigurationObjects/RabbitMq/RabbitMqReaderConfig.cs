using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Framework.Protocols.ConfigurationObjects.RabbitMq;

public record RabbitMqReaderConfig : BaseRabbitMqConfig, IReaderConfig
{
    [
        Description(
            "Use a bounded push subscription instead of BasicGet polling. Messages are acknowledged when Read receives them, not after downstream processing. Unread buffered deliveries are requeued on disconnect. False preserves polling."
        ),
        DefaultValue(false)
    ]
    public bool UseSubscription { get; set; }

    [
        Description(
            "Maximum unacknowledged deliveries and buffered messages in subscription mode. Bounds count, not bytes."
        ),
        DefaultValue(100),
        Range(1, 65535)
    ]
    public ushort PrefetchCount { get; set; } = 100;

    [
        DefaultValue(null),
        MinLength(1),
        RequiredIfAny(nameof(QueueName), null, ""),
        RequiredOrNullBasedOnOtherFieldsConfiguration(new[] { nameof(QueueName) }, false),
        Description(
            "Name of the exchange to read messages from"
                + $"Cannot be set if configured {nameof(QueueName)} to read from."
        )
    ]
    public string? ExchangeName { get; set; }

    [Description("Routing key of messages to read"), DefaultValue("/")]
    public string RoutingKey { get; set; } = "/";

    [
        DefaultValue(null),
        MinLength(1),
        RequiredIfAny(nameof(ExchangeName), null, ""),
        RequiredOrNullBasedOnOtherFieldsConfiguration(new[] { nameof(ExchangeName) }, false),
        Description(
            "Name of the queue to read messages from"
                + $"Cannot be set if configured {nameof(ExchangeName)} to read from."
        )
    ]
    public string? QueueName { get; set; } = null;

    [
        Range(0, int.MaxValue),
        Description(
            "The amount of milliseconds before the created queue is deleted when it has no readrs"
        ),
        DefaultValue(300000)
    ]
    public double CreatedQueueTimeToExpireMs { get; set; } = 300000;
}
