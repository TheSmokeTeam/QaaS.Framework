using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace QaaS.Framework.Serialization.Serializers;

/// <summary>
/// Serializes XElement/XDocument C# objects (or any xml serializable C# object) to a byte[] of xml without
/// an xml declaration
/// </summary>
public class XmlElement : ISerializer
{
    /// <inheritdoc />
    /// <remarks>
    /// XNode instances (XElement, XDocument) and strings keep the historical behavior of writing their text
    /// representation as UTF-8 bytes, and any other object is serialized with <see cref="XmlSerializer"/>
    /// (omitting the xml declaration) so typed C# objects can be serialized to xml elements directly
    /// </remarks>
    public byte[]? Serialize(object? data)
    {
        switch (data)
        {
            case null:
                return null;
            case XNode or string:
                return Encoding.UTF8.GetBytes(data.ToString() ?? string.Empty);
            default:
            {
                using var memoryStream = new MemoryStream();
                using (
                    var xmlWriter = XmlWriter.Create(
                        memoryStream,
                        new XmlWriterSettings { OmitXmlDeclaration = true }
                    )
                )
                {
                    new XmlSerializer(data.GetType()).Serialize(xmlWriter, data);
                }

                return memoryStream.ToArray();
            }
        }
    }
}
