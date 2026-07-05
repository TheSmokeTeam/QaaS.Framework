using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace QaaS.Framework.Serialization.Serializers;

/// <summary>
/// Serializes XDocument/XElement C# objects (or any xml serializable C# object) to a byte[] representing xml
/// </summary>
public class Xml : ISerializer
{
    /// <inheritdoc />
    /// <remarks>
    /// XDocument and XElement instances are written as-is, string data is treated as xml text and passed
    /// through as UTF-8 bytes, and any other object is serialized with <see cref="XmlSerializer"/> so typed
    /// C# objects can be serialized to xml directly
    /// </remarks>
    public byte[]? Serialize(object? data)
    {
        if (data is null)
            return null;
        using var memoryStream = new MemoryStream();
        switch (data)
        {
            case XDocument xDocument:
                xDocument.Save(memoryStream, SaveOptions.DisableFormatting);
                break;
            case XElement xElement:
                new XDocument(xElement).Save(memoryStream, SaveOptions.DisableFormatting);
                break;
            case string xmlText:
                return Encoding.UTF8.GetBytes(xmlText);
            default:
                new XmlSerializer(data.GetType()).Serialize(memoryStream, data);
                break;
        }

        return memoryStream.ToArray();
    }
}
