using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace QaaS.Framework.Serialization.Deserializers;

/// <summary>
/// Deserializes a byte[] of xml to an XDocument C# object by default, or to the requested C# type when a
/// deserialize type is given
/// </summary>
public class Xml: IDeserializer
{
    /// <inheritdoc />
    /// <remarks>
    /// When the deserialize type is null (or XDocument) the data is loaded into an XDocument, when it is
    /// XElement the data is parsed into an XElement, when it is string the data is decoded as UTF-8 text,
    /// and any other type is deserialized with <see cref="XmlSerializer"/> so xml payloads can be turned
    /// directly into typed C# objects
    /// </remarks>
    public object? Deserialize(byte[]? data, Type? deserializeType = null)
    {
        if (data is null) return null;
        if (deserializeType == null || deserializeType == typeof(XDocument))
        {
            using var stream = new MemoryStream(data);
            using var xmlReader = XmlReader.Create(stream);
            return XDocument.Load(xmlReader);
        }

        if (deserializeType == typeof(XElement)) return XElement.Parse(DecodeText(data));
        if (deserializeType == typeof(string)) return DecodeText(data);

        using var typedStream = new MemoryStream(data);
        return new XmlSerializer(deserializeType).Deserialize(typedStream);
    }

    /// <summary>
    /// Decodes the xml bytes as UTF-8 text, dropping a leading byte order mark when one is present
    /// (XDocument.Save emits one, and XElement.Parse rejects it as content)
    /// </summary>
    private static string DecodeText(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}