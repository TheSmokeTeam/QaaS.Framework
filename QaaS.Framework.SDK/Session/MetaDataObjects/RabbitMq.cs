using System.Text.Json.Serialization;

namespace QaaS.Framework.SDK.Session.MetaDataObjects;

/// <summary>
/// Represents the metadata of a rabbitmq message
/// </summary>
public record RabbitMq
{
    /// <summary>
    /// Application identifier from the RabbitMQ AMQP properties.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AppId { get; init; }

    /// <summary>
    /// Cluster identifier from the RabbitMQ AMQP properties.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClusterId { get; init; }

    /// <summary>
    /// Content encoding from the RabbitMQ AMQP properties.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentEncoding { get; init; }

    /// <summary>
    /// Content type from the RabbitMQ AMQP properties.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentType { get; init; }

    /// <summary>
    /// Correlation identifier from the RabbitMQ AMQP properties.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Delivery mode from the RabbitMQ AMQP properties. Valid values are 1 (transient) and 2 (persistent).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DeliveryMode { get; init; }

    /// <summary>
    /// Message expiration duration from the RabbitMQ AMQP properties.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Expiration { get; init; }

    /// <summary>
    /// Headers from the RabbitMQ AMQP properties.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, object?>? Headers { get; init; }

    /// <summary>
    /// Message identifier from the RabbitMQ AMQP properties.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MessageId { get; init; }

    /// <summary>
    /// Persistent delivery convenience flag. DeliveryMode takes precedence when both are set.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Persistent { get; init; }

    /// <summary>
    /// Priority from the RabbitMQ AMQP properties.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Priority { get; init; }

    /// <summary>
    /// Reply-to destination from the RabbitMQ AMQP properties.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReplyTo { get; init; }

    /// <summary>
    /// Routing key used when publishing or returned when consuming a RabbitMQ message.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RoutingKey { get; init; }

    /// <summary>
    /// Unix timestamp from the RabbitMQ AMQP properties.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? TimestampUnixTime { get; init; }

    /// <summary>
    /// Message type from the RabbitMQ AMQP properties.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; init; }

    /// <summary>
    /// User identifier from the RabbitMQ AMQP properties.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserId { get; init; }
}
