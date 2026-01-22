namespace QaaS.Framework.SDK.Session.MetaDataObjects;

/// <summary>
/// Represents the metadata of a kafka message
/// </summary>
public record Kafka
{
    public byte[]? MessageKey { get; init; }
    public string? TopicName { get; set; }
    public IDictionary<string, object?>? Headers { get; set; }
}