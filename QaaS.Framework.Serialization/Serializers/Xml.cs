using System.Xml.Linq;

namespace QaaS.Framework.Serialization.Serializers;

/// <summary>
/// Serializes XDocument C# object to a byte[] representing Json
/// </summary>
public class Xml: ISerializer
{
    /// <inheritdoc />
    public byte[]? Serialize(object? data)
    {
        if (data is null) return null;
        using var memoryStream = new MemoryStream();
        ((XDocument)data).Save(memoryStream, SaveOptions.DisableFormatting);
        return memoryStream.ToArray();
    }
}