using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Configurations.CommonConfigurationObjects;

/// <summary>
/// Configuration object for a MongoDB collection.
/// </summary>
public record MongoCollectionConfig
{
    [Required, Description("Connection string to the MongoDB server")]
    public string? ConnectionString { get; set; }

    [Required, Description("Name of the database to perform the operation on")]
    public string? DatabaseName { get; set; }

    [Required, Description("Name of the collection in the database to perform the operation on")]
    public string? CollectionName { get; set; }

    [Range(1, int.MaxValue), Description("Optional chunk size for MongoDB operations that process documents in batches. " +
                                         "Operations that run as a single database command may accept this shared setting without using it.")]
    public int? ChunkSize { get; set; }
}
