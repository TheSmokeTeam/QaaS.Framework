using QaaS.Framework.Serialization.Serializers;

namespace QaaS.Framework.Serialization;

/// <summary>
/// Factory to build serializers according to enum type given
/// </summary>
public static class SerializerFactory
{
    /// <summary>
    /// Build serializer object according to the given enum
    /// </summary>
    /// <param name="type"> The type of serializer to build, if null is given returns null  </param>
    /// <returns> The built serializer </returns>
    public static ISerializer? BuildSerializer(SerializationType? type)
    {
        return type switch
        {
            SerializationType.Binary => new Binary(),
            SerializationType.Json => new Json(),
            SerializationType.MessagePack => new Serializers.MessagePack(),
            SerializationType.Xml => new Xml(),
            SerializationType.Yaml => new Yaml(),
            SerializationType.ProtobufMessage => new ProtobufMessage(),
            SerializationType.XmlElement => new XmlElement(),
            null => null,
            _ => throw new ArgumentOutOfRangeException(nameof(type), 
                type, "Serializer type not supported")
        };
    }
}