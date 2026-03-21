using System.Runtime.Serialization.Formatters.Binary;

namespace QaaS.Framework.Serialization.Deserializers;

/// <summary>
/// Deserializes a byte[] to the C# object it represents
/// </summary>
public class Binary: IDeserializer
{
    /// <inheritdoc />
    public object? Deserialize(byte[]? data, Type? deserializeType = null)
    {
        if (data is null) return null;
        
        using var stream = new MemoryStream(data);
        var formatter = new BinaryFormatter();

        return formatter.Deserialize(stream);
    }
}
