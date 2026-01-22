namespace QaaS.Framework.SDK.Session.MetaDataObjects;

/// <summary>
/// Represents the metadata of a rabbitmq message
/// </summary>
public record RabbitMq
{
    public IDictionary<string, object?>? Headers { get; init; }

    public string? Expiration { get; init; }

    public string? RoutingKey { get; init; }

    public string? ContentType { get; init; }
    
    public string? Type { get; init; }
    
}