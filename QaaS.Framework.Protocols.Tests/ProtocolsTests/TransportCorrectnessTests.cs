using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Elasticsearch.Net;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Nest;
using QaaS.Framework.Protocols.ConfigurationObjects;
using QaaS.Framework.Protocols.ConfigurationObjects.MongoDb;
using QaaS.Framework.Protocols.ConfigurationObjects.Sftp;
using QaaS.Framework.Protocols.ConfigurationObjects.Socket;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.Protocols.Utils;
using QaaS.Framework.SDK.Session;
using QaaS.Framework.SDK.Session.DataObjects;
using Renci.SshNet;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class TransportCorrectnessTests
{
    [TestCase(SocketType.Stream, ProtocolType.Tcp, false)]
    [TestCase(SocketType.Dgram, ProtocolType.Udp, true)]
    public void Socket_ZeroByteReadEndsOnlyStreams(
        SocketType type,
        ProtocolType protocolType,
        bool hasMessage
    )
    {
        using var protocol = new ScriptedSocketReader(type, protocolType);
        var result = protocol.Read(TimeSpan.FromSeconds(1));
        Assert.That(result?.Body, Is.EqualTo(hasMessage ? new byte[] { 7 } : null));
    }

    private sealed class ScriptedSocketReader(SocketType type, ProtocolType protocolType)
        : SocketProtocol(
            new SocketReaderConfig { SocketType = type, ProtocolType = protocolType },
            NullLogger.Instance
        )
    {
        private int _reads;

        protected override Span<byte> GetMessage() => ++_reads == 1 ? [] : new byte[] { 7 };
    }

    [Test]
    public void Socket_PartialStreamSendsAdvanceUntilComplete()
    {
        var offsets = new List<int>();
        SocketProtocol.SendAll(
            10,
            true,
            (offset, remaining) =>
            {
                offsets.Add(offset);
                return Math.Min(3, remaining);
            }
        );
        Assert.That(offsets, Is.EqualTo(new[] { 0, 3, 6, 9 }));
        Assert.Throws<IOException>(() => SocketProtocol.SendAll(10, true, (_, _) => 0));
        Assert.Throws<IOException>(() => SocketProtocol.SendAll(10, false, (_, _) => 3));
    }

    [Test]
    public void NumericObjectNames_AreUniqueUnderConcurrency_AndStillStartAtZero()
    {
        var names = new ConcurrentBag<string>();
        var generator = new ObjectNameGenerator(
            ObjectNamingGeneratorType.GrowingNumericalSeries,
            "item-"
        );
        Parallel.For(
            0,
            10000,
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            _ => names.Add(generator.GenerateObjectName())
        );
        Assert.That(names.Distinct().Count(), Is.EqualTo(10000));
        Assert.That(names, Does.Contain("item-0"));
        Assert.That(names, Does.Contain("item-9999"));
    }

    [Test]
    public void Sftp_BufferIsOptIn_AndClientIsDisposed()
    {
        var client = new Mock<ISftpClient>();
        using (
            var protocol = new SftpProtocol(
                new SftpSenderConfig { UploadBufferSize = 65536 },
                NullLogger.Instance,
                client.Object
            )
        )
            protocol.Send(new Data<object> { Body = new byte[] { 1 } });
        client.VerifySet(x => x.BufferSize = 65536, Times.Once);
        client.Verify(x => x.Dispose(), Times.Once);
    }

    [Test]
    public void Mongo_DirectBsonAvoidsJsonConversion_AndDoesNotMutateOriginal()
    {
        var collection = new Mock<IMongoCollection<BsonDocument>>();
        var input = new BsonDocument("bytes", new BsonBinaryData(new byte[] { 1, 2 }));
        BsonDocument? sent = null;
        collection
            .Setup(x =>
                x.InsertMany(
                    It.IsAny<IEnumerable<BsonDocument>>(),
                    It.IsAny<InsertManyOptions>(),
                    default
                )
            )
            .Callback<IEnumerable<BsonDocument>, InsertManyOptions, CancellationToken>(
                (documents, options, _) =>
                {
                    Assert.That(options.IsOrdered, Is.False);
                    sent = documents.Single();
                    sent["_id"] = ObjectId.GenerateNewId();
                }
            );
        using var protocol = new MongoDbProtocol(
            new MongoDbCollectionSenderConfig { UseBsonDocumentDirectly = true, IsOrdered = false },
            NullLogger.Instance,
            collection.Object
        );
        protocol.SendChunk([new Data<object> { Body = input }]).ToArray();
        Assert.That(input.Contains("_id"), Is.False);
        Assert.That(sent!["bytes"].AsBsonBinaryData.Bytes, Is.EqualTo(new byte[] { 1, 2 }));
    }

    [Test]
    public void Elastic_JsonNodesProduceCorrectBulkJson_WithExistingNestSerializer()
    {
        byte[]? request = null;
        var response =
            "{\"took\":1,\"errors\":false,\"items\":[{\"index\":{\"_index\":\"items\",\"_id\":\"1\",\"status\":201}}]}"u8.ToArray();
        var settings = new ConnectionSettings(
            new SingleNodeConnectionPool(new Uri("http://localhost:9200")),
            new InMemoryConnection(response)
        )
            .DefaultIndex("items")
            .DisableDirectStreaming()
            .OnRequestCompleted(call => request = call.RequestBodyInBytes);
        using var protocol = new ElasticProtocol(
            new ElasticClient(settings),
            new DataFilter(),
            NullLogger.Instance
        );
        var document = JsonNode.Parse(
            "{\"message\":\"hello\",\"nested\":{\"n\":123.45},\"values\":[true,null,7]}"
        )!;
        protocol.SendChunk([new Data<object> { Body = document }]).ToArray();
        var lines = System
            .Text.Encoding.UTF8.GetString(request!)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.That(JsonNode.DeepEquals(document, JsonNode.Parse(lines[1])), Is.True);
    }

    [TestCase("1e-40")]
    [TestCase("123456789012345678901234567890.1")]
    public void Elastic_DoesNotSilentlyRoundOutOfRangeJsonNumbers(string number)
    {
        Assert.Throws<NotSupportedException>(() =>
            ElasticProtocol.NormalizeDocument(JsonNode.Parse(number))
        );
    }
}
