using System.Text.Json;
using System.Text.Json.Nodes;

namespace QaaS.Framework.Serialization.Deserializers;

/// <summary>
/// Deserializes a byte[] to a C# object representing json
/// </summary>
public class Json: IDeserializer
{
    /// <inheritdoc />
    public object? Deserialize(byte[]? data, Type? deserializeType = null)
    {
        if (data is null) return null;
        return deserializeType == null 
            ? JsonNode.Parse(data) 
            : JsonSerializer.Deserialize(data, deserializeType);
    }
}