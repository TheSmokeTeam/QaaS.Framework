using System.Reflection;
using MessagePack;

namespace QaaS.Framework.Serialization.Deserializers;

/// <summary>
/// Deserializes a byte[] to the C# object it represents
/// </summary>
public class Binary: IDeserializer
{
    /// <inheritdoc />
    public object? Deserialize(byte[]? data, Type? deserializeType = null)
    {
        if (data is null)
        {
            return null;
        }

        if (deserializeType is null)
        {
            return MessagePackSerializer.Deserialize<object?>(data, MessagePackSerializer.Typeless.DefaultOptions);
        }

        var deserializeMethod = typeof(MessagePackSerializer).GetMethod(
                                   nameof(MessagePackSerializer.Deserialize),
                                   BindingFlags.Static | BindingFlags.Public,
                                   null,
                                   new[]
                                   {
                                       typeof(ReadOnlyMemory<byte>),
                                       typeof(MessagePackSerializerOptions),
                                       typeof(CancellationToken)
                                   },
                                   null)?.MakeGenericMethod(deserializeType)
                               ?? throw new ArgumentException(
                                   "Could not create generic method for type specific binary deserialization");

        return deserializeMethod.Invoke(
            null,
            new object?[]
            {
                (ReadOnlyMemory<byte>)data,
                MessagePackSerializer.Typeless.DefaultOptions,
                default(CancellationToken)
            });
    }
}
