using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Framework.Protocols.ConfigurationObjects.RabbitMq;

public record RabbitMqSenderConfig : BaseRabbitMqConfig, ISenderConfig
{
    [DefaultValue(null), MinLength(1),
     RequiredIfAny(nameof(QueueName), null, ""),
     RequiredOrNullBasedOnOtherFieldsConfiguration(new[] { nameof(QueueName) }, false),
     Description("Name of the exchange to send messages to" +
                 $"Cannot be set if configured {nameof(QueueName)} to read from.")]
    public string? ExchangeName { get; set; } = null;

    [Description("Default routing key to send mesages to the exchange with, if the message" +
                 " doesn't contain any RoutingKey in its MetaData this routing key is used"), DefaultValue("/")]
    public string RoutingKey { get; set; } = "/";

    [DefaultValue(null), MinLength(1),
     RequiredIfAny(nameof(ExchangeName), null, ""),
     RequiredOrNullBasedOnOtherFieldsConfiguration(new[] { nameof(ExchangeName) }, false),
     Description("Name of the queue to send messages to. " +
                 $"Cannot be set if configured {nameof(ExchangeName)} to read from.")]
    public string? QueueName { get; set; } = null;

    [Description("Default Headers to send messages with, if the message" +
                 " doesn't contain any Headers in its MetaData these Headers are used"), DefaultValue(null)]
    public Dictionary<string, object?>? Headers { get; set; } = null;

    [Description("Default ContentType to send messages with, if the message" +
                 " doesn't contain ContentType in its MetaData this one is Used"), DefaultValue(null)]
    public string? ContentType { get; set; } = null;

    [Description("Default Type to send messages with, if the message" +
                 " doesn't contain Type in its MetaData this one is Used"), DefaultValue(null)]
    public string? Type { get; set; } = null;

    [Description("Default Message expiration duration to send messages with, if the message" +
                 " doesn't contain any Expiration in its MetaData this Expiration is used"), DefaultValue(null)]
    public string? Expiration { get; set; } = null;
};
