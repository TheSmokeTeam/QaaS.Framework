using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using QaaS.Framework.Protocols.ConfigurationObjects.MongoDb;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.Protocols.Protocols;

public class MongoDbProtocol(MongoDbCollectionSenderConfig configuration, ILogger logger) : IChunkSender
{
    // The client that holds all the Db's in the mongo server
    private IMongoCollection<BsonDocument>? _mongoCollection;


    public SerializationType? GetSerializationType() => SerializationType.Json;

    public IEnumerable<DetailedData<object>> SendChunk(IEnumerable<Data<object>> chunkDataToSend)
    {
        var dataToSend = chunkDataToSend.ToList();
        if (!dataToSend.Any()) return [];
        var chunkInsertionTime = DateTime.UtcNow;
        // create the documents to send in one time
        var bsonDocuments = dataToSend.Select(message =>
        {
            var json = JsonSerializer.Serialize(message.Body);
            var bsonDocument = BsonDocument.Parse(json);

            return bsonDocument;
        });

        try
        {
            _mongoCollection!.InsertMany(bsonDocuments);
        }
        catch (Exception ex)
        {
            logger.LogError("Error while sending chunk to MongoDB collection {CollectionName}: {Exception}",
                configuration.CollectionName, ex);
            throw;
        }

        logger.LogDebug("Finished sending chunk");
        return dataToSend.Select(message => message.CloneDetailed(chunkInsertionTime)).ToImmutableList();
    }

    public void Connect()
    {
        var mongoClient = new MongoClient(configuration.ConnectionString);
        var mongoDb = mongoClient.GetDatabase(configuration.DatabaseName);
        _mongoCollection = mongoDb.GetCollection<BsonDocument>(configuration.CollectionName);
    }

    public void Disconnect()
    {
    }
}