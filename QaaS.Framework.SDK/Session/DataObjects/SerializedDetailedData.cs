using QaaS.Framework.Serialization;

namespace QaaS.Framework.SDK.Session.DataObjects;

/// <summary>
/// The serialized appearance of the DetailedData record.
/// </summary>
public record SerializedDetailedData : DetailedData<byte[]>
{
    /// <summary>
    /// The type the data can be deserialized to
    /// </summary>
    public SpecificTypeConfig? Type { get; init; }
}