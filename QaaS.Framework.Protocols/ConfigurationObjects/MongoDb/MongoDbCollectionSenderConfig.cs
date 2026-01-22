using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Protocols.ConfigurationObjects.MongoDb;

public record MongoDbCollectionSenderConfig : ISenderConfig
{
    [Required, Description("The connection string to the MongoDb server")]
    public string? ConnectionString { get; set; }
        
    [Required, Description("The DB to insert data to")]
    public string? DatabaseName { get; set; }
    
    [Required, Description("The collection to insert data to")]
    public string? CollectionName { get; set; }

    // [Range(1, int.MaxValue), Description("The size of the chunks to insert to the mongo collection," +
    //                                      " chunk represents the number of documents inserted in every insert to collection function." +
    //                                      " If no chunksize is given inserts all data as a single chunk.")]
    // public int? ChunkSize { get; set; }
}