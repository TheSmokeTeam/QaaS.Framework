using System.Text.Json;

namespace QaaS.Framework.Serialization.Serializers;

/// <summary>
/// Serializes any C# object to a byte[] representing Json.
/// ⚠ does not work well with JToken, if you want to serialize a json object use JsonNode from System.Text.Json
/// </summary>
public class Json: ISerializer
{
    /// <inheritdoc />
    public byte[]? Serialize(object? data)
    {
        return data is null ? null : JsonSerializer.SerializeToUtf8Bytes(data);
    }
}