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

    [Range(1, int.MaxValue), Description("Chunk size of the data to process, " +
                                         "This represents the number of documents to process in a single operation. " +
                                         "If not specified, all data will be processed in a single chunk.")]
    public int? ChunkSize { get; set; }
}