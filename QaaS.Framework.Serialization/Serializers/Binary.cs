using MessagePack;

namespace QaaS.Framework.Serialization.Serializers;

/// <summary>
/// Serializes Serializable C# object to a byte[] representing the object
/// </summary>
public class Binary: ISerializer
{
    /// <inheritdoc />
    public byte[]? Serialize(object? data)
        => data is null
            ? null
            : MessagePackSerializer.Serialize<object?>(data, MessagePackSerializer.Typeless.DefaultOptions);
}
