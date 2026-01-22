using Google.Protobuf;

namespace QaaS.Framework.Serialization.Serializers;

/// <summary>
/// Serializes Serializable C# Protobuf Message object to a byte[] representing the object
/// </summary>
public class ProtobufMessage: ISerializer
{
    /// <inheritdoc />
    public byte[]? Serialize(object? data)
    {
        return ((IMessage?)data)?.ToByteArray();
    }
}