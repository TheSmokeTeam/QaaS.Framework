using System.Text;

namespace QaaS.Framework.Serialization.Serializers;

/// <summary>
/// Serializes from an XElement
/// </summary>
public class XmlElement: ISerializer
{
    /// <inheritdoc />
    public byte[]? Serialize(object? data)
    {
        return data is null 
            ? null 
            : Encoding.UTF8.GetBytes(data.ToString());
    }
}