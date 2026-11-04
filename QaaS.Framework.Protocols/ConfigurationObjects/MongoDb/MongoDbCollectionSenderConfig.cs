using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Protocols.ConfigurationObjects.MongoDb;

public record MongoDbCollectionSenderConfig : ISenderConfig
{
    [
        Description(
            "Keep ordered InsertMany behavior. False permits unordered insertion and can change partial-failure/order semantics."
        ),
        DefaultValue(true)
    ]
    public bool IsOrdered { get; set; } = true;

    [Description(
        "Optional MongoDB document-validation bypass. When omitted, the driver/server default applies."
    )]
    public bool? BypassDocumentValidation { get; set; }

    [
        Description(
            "For BsonDocument bodies, clone and insert BSON directly without a JSON round trip. Other body types retain JSON conversion. When false, all bodies are converted through JSON."
        ),
        DefaultValue(false)
    ]
    public bool UseBsonDocumentDirectly { get; set; }

    [Required, Description("The connection string to the MongoDb server")]
    public string? ConnectionString { get; set; }

    [Required, Description("The DB to insert data to")]
    public string? DatabaseName { get; set; }

    [Required, Description("The collection to insert data to")]
    public string? CollectionName { get; set; }
}
