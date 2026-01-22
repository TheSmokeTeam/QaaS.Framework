using System.Reflection;
using MessagePack;

namespace QaaS.Framework.Serialization.Deserializers;

/// <summary>
/// Deserializes a byte[] to a C# object serializable to messagepack
/// </summary>
public class MessagePack: IDeserializer
{
    /// <inheritdoc />
    public object? Deserialize(byte[]? data, Type? deserializeType = null)
    {
        if (data is null) return null;
        return deserializeType != null 
            ? (typeof(MessagePackSerializer).GetMethod(nameof(MessagePackSerializer.Deserialize), 
                      BindingFlags.Static | BindingFlags.Public,
                      null, new[]{typeof(ReadOnlyMemory<byte>),
                          typeof(MessagePackSerializerOptions), typeof(CancellationToken)},
                      null)?
                  .MakeGenericMethod(deserializeType) ??
              throw new ArgumentException("Could not create generic method for type specific deserialization method"))
            .Invoke(null, new object?[] { (ReadOnlyMemory<byte>)data, null, default }) 
            : MessagePackSerializer.Deserialize<object?>(data);
    }
}