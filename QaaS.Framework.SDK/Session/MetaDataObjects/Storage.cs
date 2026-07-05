using System.Text.Json.Serialization;

namespace QaaS.Framework.SDK.Session.MetaDataObjects;

/// <summary>
/// Represents the metadata of a storage item (could be S3 or FileSystem)
/// </summary>
public record Storage
{
    /// <summary>
    /// The key identifier of the storage item
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// User-defined headers or metadata attached to the storage item.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string>? Headers { get; init; }
}
