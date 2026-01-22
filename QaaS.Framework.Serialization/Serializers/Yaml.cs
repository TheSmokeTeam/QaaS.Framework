using System.Text;
using YamlDotNet.Serialization;

namespace QaaS.Framework.Serialization.Serializers;

/// <summary>
/// Serializes any C# object to a byte[] representing yaml
/// </summary>
public class Yaml: ISerializer
{
    private readonly YamlDotNet.Serialization.ISerializer _serializer = new SerializerBuilder().Build();

    /// <inheritdoc />
    public byte[]? Serialize(object? data)
    {
        return data is null ? null : Encoding.UTF8.GetBytes(_serializer.Serialize(data));
    }
}