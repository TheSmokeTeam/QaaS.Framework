using YamlDotNet.Serialization;

namespace QaaS.Framework.Serialization.Deserializers;

/// <summary>
/// Deserializes a byte[] to a C# object representing yaml
/// </summary>
public class Yaml: IDeserializer
{
    private readonly YamlDotNet.Serialization.IDeserializer _deserializer = new DeserializerBuilder().Build();

    /// <inheritdoc />
    public object? Deserialize(byte[]? data, Type? deserializeType = null)
    {
        if (data is null) return null;
        using var stream = new MemoryStream(data);
        return deserializeType != null 
            ? _deserializer.Deserialize(new StreamReader(stream), deserializeType) 
            : _deserializer.Deserialize(new StreamReader(stream));
    }
}