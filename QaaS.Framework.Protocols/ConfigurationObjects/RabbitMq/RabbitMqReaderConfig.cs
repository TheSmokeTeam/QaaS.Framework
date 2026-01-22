using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Framework.Protocols.ConfigurationObjects.RabbitMq;

public record RabbitMqReaderConfig : BaseRabbitMqConfig, IReaderConfig
{
    [DefaultValue(null),
     RequiredIfAny(nameof(QueueName), [null]),
     Description("Name of the exchange to read messages from" +
                 $"Cannot be set if configured {nameof(QueueName)} to read from.")]
    public string? ExchangeName { get; set; }

    [Description("Routing key of messages to read"), DefaultValue("/")]
    public string RoutingKey { get; set; } = "/";

    [DefaultValue(null),
     RequiredIfAny(nameof(ExchangeName), [null]),
     Description("Name of the queue to read messages from" +
                 $"Cannot be set if configured {nameof(ExchangeName)} to read from.")]
    public string? QueueName { get; set; } = null;

    [Range(0, int.MaxValue), Description(
         "The amount of milliseconds before the created queue is deleted when it has no readrs"), DefaultValue(300000)]
    public double CreatedQueueTimeToExpireMs { get; set; } = 300000;
}