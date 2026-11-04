using System.Reflection;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QaaS.Framework.Protocols.ConfigurationObjects.S3;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.Protocols.Utils.S3Utils;
using QaaS.Framework.SDK.Session;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class S3IncrementalReadTests
{
    [Test]
    public void ReadChunk_DoesNotDownloadBodiesUntilEnumerated()
    {
        var getCalls = 0;
        var objects = Enumerable
            .Range(0, 5)
            .Select(i => new S3Object
            {
                Key = i.ToString(),
                Size = 1,
                LastModified = DateTime.UtcNow.AddMinutes(-1),
            })
            .ToArray();
        var client = new Mock<IS3Client>();
        client.SetupGet(x => x.Client).Returns(new Mock<IAmazonS3>().Object);
        client
            .Setup(x => x.ListAllObjectsInS3Bucket("bucket", "", "", It.IsAny<bool>()))
            .ReturnsAsync(objects);
        client
            .Setup(x => x.GetAllObjectsInS3BucketUnOrdered("bucket", "", "", false))
            .Returns(() =>
                objects
                    .Select(item =>
                    {
                        getCalls++;
                        return new KeyValuePair<S3Object, byte[]?>(item, [1]);
                    })
                    .ToArray()
            );
        client
            .Setup(x => x.GetObjectFromObjectMetadata(It.IsAny<S3Object>(), "bucket"))
            .Returns(
                (S3Object item, string _) =>
                {
                    getCalls++;
                    return new KeyValuePair<S3Object, byte[]?>(item, [1]);
                }
            );
        using var protocol = new S3Protocol(
            new S3BucketReaderConfig { StorageBucket = "bucket", StreamResults = true },
            new DataFilter(),
            NullLogger.Instance
        );
        typeof(S3Protocol)
            .GetField("_s3Client", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(protocol, client.Object);

        var result = protocol.ReadChunk(TimeSpan.Zero);

        Assert.That(getCalls, Is.Zero, "ReadChunk must return before body acquisition");
        using var iterator = result.GetEnumerator();
        Assert.That(iterator.MoveNext(), Is.True);
        Assert.That(getCalls, Is.EqualTo(1), "Default degree must fetch just the requested item");
    }
}
