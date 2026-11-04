using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
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
public class S3IncrementalReadCoverageTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc);

    private static S3Object[] Objects(int count) =>
        Enumerable
            .Range(0, count)
            .Select(i => new S3Object
            {
                Key = $"o{i}",
                Size = 10,
                LastModified = Now.AddSeconds(i - count),
            })
            .ToArray();

    private static byte[] Bytes(string key) => Encoding.UTF8.GetBytes($"body:{key}");

    private sealed class ClockProtocol(S3BucketReaderConfig config, DataFilter filter)
        : S3Protocol(config, filter, NullLogger.Instance)
    {
        public DateTime Clock { get; set; } = Now;

        protected override DateTime GetCurrentDateTimeUtc() => Clock;
    }

    private sealed class Fixture : IDisposable
    {
        public S3BucketReaderConfig Config { get; } =
            new() { StorageBucket = "bucket", StreamResults = true };
        public DataFilter Filter { get; } = new();
        public Mock<IAmazonS3> Amazon { get; } = new(MockBehavior.Strict);
        public Mock<IS3Client> Client { get; } = new();
        public ClockProtocol Protocol { get; }
        public ConcurrentQueue<string> Keys { get; } = new();
        public ConcurrentQueue<string> Buckets { get; } = new();
        public Func<S3Object, byte[]?> Load { get; set; } = item => Bytes(item.Key);
        public int Active;
        public int Lists;

        public Fixture(S3Object[] objects, int degree = 1)
        {
            Config.MaxConcurrentDownloads = degree;
            Client.SetupGet(x => x.Client).Returns(Amazon.Object);
            Client
                .Setup(x =>
                    x.ListAllObjectsInS3Bucket(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<bool>()
                    )
                )
                .Returns(
                    (string _, string _, string _, bool skip) =>
                    {
                        Interlocked.Increment(ref Lists);
                        return Task.FromResult<IEnumerable<S3Object>>(
                            skip ? objects.Where(x => x.Size > 0) : objects
                        );
                    }
                );
            Client
                .Setup(x => x.GetObjectFromObjectMetadata(It.IsAny<S3Object>(), It.IsAny<string>()))
                .Returns(
                    (S3Object item, string bucket) =>
                    {
                        Keys.Enqueue(item.Key);
                        Buckets.Enqueue(bucket);
                        Interlocked.Increment(ref Active);
                        try
                        {
                            return new KeyValuePair<S3Object, byte[]?>(item, Load(item));
                        }
                        finally
                        {
                            Interlocked.Decrement(ref Active);
                        }
                    }
                );
            Protocol = new ClockProtocol(Config, Filter);
            typeof(S3Protocol)
                .GetField("_s3Client", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Protocol, Client.Object);
        }

        public void Dispose() => Protocol.Dispose();
    }

    [Test]
    public void LegacyDefault_DownloadsBeforeReturning_AndRepeatedEnumerationUsesTheSnapshot()
    {
        using var f = new Fixture(Objects(3));
        f.Config.StreamResults = new S3BucketReaderConfig().StreamResults;
        var result = f.Protocol.ReadChunk(TimeSpan.Zero);
        Assert.That(f.Keys.Count, Is.EqualTo(3));
        var first = result.ToArray();
        var second = result.ToArray();
        Assert.That(f.Keys.Count, Is.EqualTo(3));
        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void LegacyDefault_ReportsBodyFailureDuringReadChunk()
    {
        using var f = new Fixture(Objects(3));
        f.Config.StreamResults = false;
        f.Load = _ => throw new IOException("body failure");
        Assert.Throws<IOException>(() => f.Protocol.ReadChunk(TimeSpan.Zero));
    }

    [TestCase(1)]
    [TestCase(4)]
    [TestCase(int.MaxValue)]
    public void OrderedWindows_PreserveDistinctBytes_AndStopAtFirstWindow(int degree)
    {
        var objects = Objects(9);
        using var f = new Fixture(objects.Reverse().ToArray(), degree);
        var sequence = f.Protocol.ReadChunk(TimeSpan.Zero);
        using (var iterator = sequence.GetEnumerator())
        {
            Assert.That(iterator.MoveNext(), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(iterator.Current.MetaData?.Storage?.Key, Is.EqualTo("o0"));
                Assert.That(iterator.Current.Body, Is.EqualTo(Bytes("o0")));
                Assert.That(f.Keys.Count, Is.EqualTo(Math.Min(degree, 9)));
                Assert.That(f.Active, Is.Zero);
            });
        }
        Assert.That(f.Keys.Count, Is.EqualTo(Math.Min(degree, 9)));
        var all = sequence.ToArray();
        Assert.That(
            all.Select(x => x.MetaData?.Storage?.Key),
            Is.EqualTo(objects.Select(x => x.Key))
        );
        foreach (var item in all)
            Assert.That(item.Body, Is.EqualTo(Bytes(item.MetaData!.Storage!.Key!)));
    }

    [Test]
    public void FourWorkers_RunConcurrently_AndFinishBeforeYield()
    {
        using var f = new Fixture(Objects(12), 4);
        using var entered = new CountdownEvent(4);
        using var release = new ManualResetEventSlim();
        f.Load = item =>
        {
            entered.Signal();
            if (!release.Wait(TimeSpan.FromSeconds(15)))
                throw new TimeoutException();
            return Bytes(item.Key);
        };
        var sequence = f.Protocol.ReadChunk(TimeSpan.Zero);
        var first = Task.Run(() => sequence.Take(1).Single());
        var allEntered = entered.Wait(TimeSpan.FromSeconds(10));
        var active = f.Active;
        release.Set();
        var item = first.GetAwaiter().GetResult();
        Assert.Multiple(() =>
        {
            Assert.That(allEntered, Is.True);
            Assert.That(active, Is.EqualTo(4));
            Assert.That(f.Keys.Count, Is.EqualTo(4));
            Assert.That(f.Active, Is.Zero);
            Assert.That(item.MetaData?.Storage?.Key, Is.EqualTo("o0"));
        });
    }

    [TestCase(1)]
    [TestCase(4)]
    public void FailedLaterWindow_PropagatesWithoutLeavingWorkersOrReadingFutureWindows(int degree)
    {
        using var f = new Fixture(Objects(12), degree);
        f.Load = item =>
            item.Key == "o4"
                ? throw new InvalidOperationException("failed-object-o4")
                : Bytes(item.Key);
        var seen = new List<string?>();
        var error = Assert.Catch(() =>
        {
            foreach (var item in f.Protocol.ReadChunk(TimeSpan.Zero))
                seen.Add(item.MetaData?.Storage?.Key);
        });
        Assert.Multiple(() =>
        {
            Assert.That(error!.ToString(), Does.Contain("failed-object-o4"));
            Assert.That(seen, Is.EqualTo(new[] { "o0", "o1", "o2", "o3" }));
            Assert.That(f.Keys.All(x => int.Parse(x[1..]) < (degree == 1 ? 5 : 8)), Is.True);
            Assert.That(f.Active, Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Failure_WaitsForBlockedSiblingBeforeReturning(bool failInHeader)
    {
        using var f = new Fixture(Objects(6), 2);
        using var siblingEntered = new ManualResetEventSlim();
        using var failureReached = new ManualResetEventSlim();
        using var releaseSibling = new ManualResetEventSlim();
        var activeOperations = 0;
        byte[] RunOperation(string key)
        {
            Interlocked.Increment(ref activeOperations);
            try
            {
                if (key == "o0")
                {
                    if (!siblingEntered.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException("Sibling did not start");
                    failureReached.Set();
                    throw new InvalidOperationException("joined-worker-failure");
                }
                siblingEntered.Set();
                if (!releaseSibling.Wait(TimeSpan.FromSeconds(15)))
                    throw new TimeoutException("Sibling was not released");
                return Bytes(key);
            }
            finally
            {
                Interlocked.Decrement(ref activeOperations);
            }
        }

        if (failInHeader)
        {
            f.Config.ReadStorageHeaders = true;
            f.Amazon.Setup(x =>
                    x.GetObjectMetadataAsync(
                        "bucket",
                        It.IsAny<string>(),
                        null,
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(
                    (string _, string key, string _, CancellationToken _) =>
                    {
                        RunOperation(key);
                        return Task.FromResult(new GetObjectMetadataResponse());
                    }
                );
        }
        else
            f.Load = item => RunOperation(item.Key);

        var call = Task.Run(() =>
        {
            try
            {
                f.Protocol.ReadChunk(TimeSpan.Zero).ToArray();
                return (Exception?)null;
            }
            catch (Exception error)
            {
                return error;
            }
        });
        bool reached;
        bool completedWhileBlocked;
        try
        {
            reached = failureReached.Wait(TimeSpan.FromSeconds(10));
            completedWhileBlocked = call.Wait(TimeSpan.FromMilliseconds(100));
        }
        finally
        {
            releaseSibling.Set();
        }
        var failure = call.GetAwaiter().GetResult();
        Assert.Multiple(() =>
        {
            Assert.That(reached, Is.True);
            Assert.That(completedWhileBlocked, Is.False, "Failure must join the blocked sibling");
            Assert.That(failure?.ToString(), Does.Contain("joined-worker-failure"));
            Assert.That(activeOperations, Is.Zero);
            Assert.That(f.Active, Is.Zero);
            Assert.That(f.Keys.Order(), Is.EqualTo(new[] { "o0", "o1" }));
        });
    }

    [Test]
    public void CallTimeSettingsAndObjectMetadata_AreSnapshots_AndRepeatedEnumerationRefetches()
    {
        var objects = Objects(3);
        using var f = new Fixture(objects);
        var sequence = f.Protocol.ReadChunk(TimeSpan.Zero);
        f.Config.StorageBucket = "changed";
        f.Config.MaxConcurrentDownloads = 4;
        f.Config.ReadStorageHeaders = true;
        f.Filter.Body = false;
        objects[0].Key = "changed";
        objects[0].LastModified = Now.AddHours(1);
        objects[0].Size = 0;
        var first = sequence.Take(1).Single();
        Assert.Multiple(() =>
        {
            Assert.That(first.MetaData?.Storage?.Key, Is.EqualTo("o0"));
            Assert.That(first.Body, Is.EqualTo(Bytes("o0")));
            Assert.That(f.Keys.Count, Is.EqualTo(1));
        });
        Assert.That(sequence.Count(), Is.EqualTo(3));
        Assert.That(f.Keys.Count, Is.EqualTo(4));
        Assert.That(f.Buckets.All(x => x == "bucket"), Is.True);
        Assert.That(
            f.Lists,
            Is.EqualTo(2),
            "Inactivity probe and metadata snapshot only; enumeration must not relist"
        );
    }

    [Test]
    public void RunStartAndMissingTimestamp_AreFilteredBeforeGet_AndSurviveAnotherReadCall()
    {
        var objects = Objects(3);
        objects[1].LastModified = Now;
        objects[2].LastModified = null;
        using var f = new Fixture(objects);
        f.Config.ReadFromRunStartTime = true;
        var sequence = f.Protocol.ReadChunk(TimeSpan.Zero);
        f.Protocol.Clock = Now.AddHours(1);
        var second = f.Protocol.ReadChunk(TimeSpan.Zero);
        Assert.That(second, Is.Empty);
        Assert.That(sequence.Single().MetaData?.Storage?.Key, Is.EqualTo("o1"));
        Assert.That(f.Keys, Is.EqualTo(new[] { "o1" }));
    }

    [TestCase(true, true, 1)]
    [TestCase(true, false, 2)]
    [TestCase(false, true, 1)]
    [TestCase(false, false, 2)]
    public void EmptyObjectsAndBodyFilter_HonorSettingsWithoutUnnecessaryGets(
        bool body,
        bool skip,
        int expected
    )
    {
        var objects = Objects(2);
        objects[0].Size = 0;
        using var f = new Fixture(objects);
        f.Filter.Body = body;
        f.Config.SkipEmptyObjects = skip;
        var results = f.Protocol.ReadChunk(TimeSpan.Zero).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(results.Length, Is.EqualTo(expected));
            Assert.That(f.Keys, Is.EqualTo(body ? new[] { "o1" } : Array.Empty<string>()));
            Assert.That(
                results.Where(x => x.MetaData?.Storage?.Key == "o0").All(x => x.Body == null),
                Is.True
            );
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Headers_AreOptionalNormalizedAndCapturedAtCallTime(bool body)
    {
        using var f = new Fixture(Objects(1));
        f.Filter.Body = body;
        f.Config.ReadStorageHeaders = true;
        var response = new GetObjectMetadataResponse();
        response.Metadata["x-amz-meta-trace"] = "value";
        f.Amazon.Setup(x =>
                x.GetObjectMetadataAsync("bucket", "o0", null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(response);
        var sequence = f.Protocol.ReadChunk(TimeSpan.Zero);
        f.Config.ReadStorageHeaders = false;
        f.Config.StorageBucket = "changed";
        var item = sequence.Single();
        Assert.That(item.MetaData!.Storage!.Headers!["trace"], Is.EqualTo("value"));
        f.Amazon.Verify(
            x => x.GetObjectMetadataAsync("bucket", "o0", null, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Test]
    public void HeaderFailure_PropagatesAfterAllWindowWorkersFinish()
    {
        using var f = new Fixture(Objects(8), 4);
        f.Config.ReadStorageHeaders = true;
        f.Amazon.Setup(x =>
                x.GetObjectMetadataAsync(
                    "bucket",
                    It.IsAny<string>(),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new AmazonS3Exception("header-failure"));
        var error = Assert.Catch(() => f.Protocol.ReadChunk(TimeSpan.Zero).ToArray());
        Assert.That(error!.ToString(), Does.Contain("header-failure"));
        Assert.That(f.Active, Is.Zero);
        Assert.That(f.Keys.All(x => int.Parse(x[1..]) < 4), Is.True);
    }

    [Test]
    public void EqualTimestampsPreserveListingOrder_AndNullNonemptyBodiesAreSkipped()
    {
        var objects = Objects(3);
        foreach (var item in objects)
            item.LastModified = Now;
        using var f = new Fixture(objects.Reverse().ToArray(), 4);
        f.Load = item => item.Key == "o1" ? null : Bytes(item.Key);
        Assert.That(
            f.Protocol.ReadChunk(TimeSpan.Zero).Select(x => x.MetaData?.Storage?.Key),
            Is.EqualTo(new[] { "o2", "o0" })
        );
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void NonPositiveDownloadDegree_IsRejectedByValidationAndDirectCalls(int degree)
    {
        using var f = new Fixture(Objects(1), degree);
        var context = new ValidationContext(f.Config)
        {
            MemberName = nameof(S3BucketReaderConfig.MaxConcurrentDownloads),
        };
        Assert.That(
            Validator.TryValidateProperty(degree, context, new List<ValidationResult>()),
            Is.False
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => f.Protocol.ReadChunk(TimeSpan.Zero));
        Assert.That(f.Lists, Is.Zero);
    }

    [Test]
    public void EmptySnapshot_DoesNotDownload_AndDefaultDegreeIsOne()
    {
        using var f = new Fixture([]);
        Assert.That(new S3BucketReaderConfig().MaxConcurrentDownloads, Is.EqualTo(1));
        Assert.That(f.Protocol.ReadChunk(TimeSpan.Zero), Is.Empty);
        Assert.That(f.Keys, Is.Empty);
    }
}
