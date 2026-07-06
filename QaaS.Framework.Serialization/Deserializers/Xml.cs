using System.Xml;
using System.Xml.Linq;

namespace QaaS.Framework.Serialization.Deserializers;

/// <summary>
/// Deserializes a byte[] to an XDocument C# object representing xml
/// </summary>
public class Xml: IDeserializer
{
    /// <inheritdoc />
    public object? Deserialize(byte[]? data, Type? deserializeType = null)
    {
        if (data is null) return null;
        using var stream = new MemoryStream(data);
        using var xmlReader = XmlReader.Create(stream);
        return XDocument.Load(xmlReader);
    }
}