using System.Runtime.Serialization.Formatters.Binary;

namespace QaaS.Framework.Serialization.Serializers;

/// <summary>
/// Serializes Serializable C# object to a byte[] representing the object
/// </summary>
public class Binary: ISerializer
{
    /// <inheritdoc />
    public byte[]? Serialize(object? data)
    {
        if (data is null) return null;
        
        using var memoryStream = new MemoryStream();
        var formatter = new BinaryFormatter();
        formatter.Serialize(memoryStream, data);

        return memoryStream.ToArray();
    }
}