using System.Text;
using System.Xml.Linq;

namespace QaaS.Framework.Serialization.Deserializers;

/// <summary>
/// Deserializes to an XElement from an xml string
/// </summary>
public class XmlElement: IDeserializer
{
    /// <inheritdoc />
    public object? Deserialize(byte[]? data, Type? deserializeType = null)
    {
        return data is null 
            ? null 
            : XElement.Parse(Encoding.UTF8.GetString(data));
    }
}