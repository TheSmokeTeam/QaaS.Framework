namespace QaaS.Framework.Serialization;

/// <summary>
/// All serialization (serializer/deserializer) supported types
/// </summary>
public enum SerializationType
{
    Binary,
    Json,
    MessagePack,
    Xml,
    Yaml,
    ProtobufMessage,
    XmlElement
}