using QaaS.Framework.Serialization.Deserializers;
using QaaS.Framework.Serialization.Serializers;

namespace QaaS.Framework.SDK.Session.MetaDataObjects;

/// <summary>
/// Contains all types of metadata that can be attached to a session data item
/// </summary>
public record MetaData
{
    /// <summary>
    /// Index of the data used to match it with another input or output date item
    /// </summary>
    public int? IoMatchIndex { get; init; }

    public Kafka? Kafka { get; init; }
    public RabbitMq? RabbitMq { get; init; }
    public Http? Http { get; init; }
    public Redis? Redis { get; init; }
    public Storage? Storage { get; init; }

    /// <summary>
    /// The Serializer that will serialize the generated data, null means no serialization.
    /// </summary>
    private readonly ISerializer? _serializer;

    public ISerializer? Serializer
    {
        get => _serializer;
        init
        {
            if (value is not null && _deserializer is not null)
                throw new InvalidOperationException(
                    $"{nameof(MetaData)} object can't have both Serializer and Deserializer");
            _serializer = value;
        }
    }

    /// <summary>
    /// The Deserializer that will deserialize the generated data, null means no deserialization.
    /// </summary>
    private readonly IDeserializer? _deserializer;

    public IDeserializer? Deserializer
    {
        get => _deserializer;
        init
        {
            if (value is not null && _serializer is not null)
                throw new InvalidOperationException(
                    $"{nameof(MetaData)} object can't have both Serializer and Deserializer");
            _deserializer = value;
        }
    }
}