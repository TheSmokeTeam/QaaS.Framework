using Google.Protobuf;

namespace QaaS.Framework.Serialization.Deserializers;

/// <summary>
/// Deserializes a byte[] to the C# Protobuf Message object it represents
/// </summary>
public class ProtobufMessage: IDeserializer
{
    /// <inheritdoc />
    public object? Deserialize(byte[]? data, Type? deserializeType)
    {
        if (data is null) return null;
        if (deserializeType == null) throw new ArgumentException(
            "ProtobufMessage deserialization is not possible without specifying " +
            "a specific Protobuf Message object to deserialize to");
        using var dataMemoryStream = new MemoryStream(data);
        var deserializedData = (IMessage)Activator.CreateInstance(deserializeType, true)!;
        deserializedData.MergeFrom(dataMemoryStream);
        return deserializedData;
    }
}