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
}