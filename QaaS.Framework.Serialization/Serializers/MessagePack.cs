using MessagePack;

namespace QaaS.Framework.Serialization.Serializers;

/// <summary>
/// Serializes any messagepack serializable C# object to a byte[] representing the messagepack
/// </summary>
public class MessagePack: ISerializer
{
    /// <inheritdoc />
    public byte[]? Serialize(object? data)
    {
        return data is null ? null : MessagePackSerializer.Serialize(data);
    }
}