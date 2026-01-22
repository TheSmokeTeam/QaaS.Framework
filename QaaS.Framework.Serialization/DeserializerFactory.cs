using QaaS.Framework.Serialization.Deserializers;
using IDeserializer = QaaS.Framework.Serialization.Deserializers.IDeserializer;

namespace QaaS.Framework.Serialization;

/// <summary>
/// Factory to build deserializers according to enum type given
/// </summary>
public static class DeserializerFactory
{
    /// <summary>
    /// Build deserializer object according to the given enum
    /// </summary>
    /// <param name="type"> The type of deserializer to build, if null is given returns null  </param>
    /// <returns> The built deserializer </returns>
    public static IDeserializer? BuildDeserializer(SerializationType? type)
    {
        return type switch
        {
            SerializationType.Binary => new Binary(),
            SerializationType.Json => new Json(),
            SerializationType.MessagePack => new Deserializers.MessagePack(),
            SerializationType.Xml => new Xml(),
            SerializationType.Yaml => new Yaml(),
            SerializationType.ProtobufMessage => new ProtobufMessage(),
            SerializationType.XmlElement => new XmlElement(),
            null => null,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, 
                "Deserializer type not supported")
        };
    }
}