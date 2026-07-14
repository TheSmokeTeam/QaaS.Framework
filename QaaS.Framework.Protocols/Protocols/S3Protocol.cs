using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Protocols.ConfigurationObjects.S3;
using QaaS.Framework.Protocols.Extentions;
using QaaS.Framework.Protocols.Utils;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Session;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Framework.Serialization;
using IS3Client = QaaS.Framework.Protocols.Utils.S3Utils.IS3Client;
using S3Client = QaaS.Framework.Protocols.Utils.S3Utils.S3Client;

namespace QaaS.Framework.Protocols.Protocols;

public class S3Protocol : IChunkReader, ISender, IDisposable
{
    private const string S3UserMetadataPrefix = "x-amz-meta-";
    private readonly ILogger _logger;
    private IS3Client? _s3Client;
    private readonly S3BucketSenderConfig? _senderConfig;
    private readonly S3BucketReaderConfig? _readerConfig;
    private readonly DataFilter _dataFilter = new();
    private DateTime? _readStartTimeUtc;
    public ObjectNameGenerator Generator { get; set; } = null!;

    protected virtual DateTime GetCurrentDateTimeUtc() => DateTime.UtcNow;

    public S3Protocol(S3BucketSenderConfig configuration, ILogger logger)
    {
        _logger = logger;
        Generator = new ObjectNameGenerator(
            configuration.S3SentObjectsNaming,
            configuration.Prefix
        );
        _senderConfig = configuration;
    }

    public S3Protocol(S3BucketReaderConfig configuration, DataFilter dataFilter, ILogger logger)
    {
        _logger = logger;
        _dataFilter = dataFilter;
        _readerConfig = configuration;
    }

    public SerializationType? GetSerializationType() => null;

    public IEnumerable<DetailedData<object>> ReadChunk(TimeSpan timeout)
    {
        if (_readerConfig!.ReadFromRunStartTime)
            _readStartTimeUtc = GetCurrentDateTimeUtc();

        _logger.LogInformation(
            "Starting S3 read from bucket {BucketName}. Prefix: {Prefix}. Delimiter: {Delimiter}. IncludeBody: {IncludeBody}. SkipEmptyObjects: {SkipEmptyObjects}. ReadFromRunStartTime: {ReadFromRunStartTime}.",
            _readerConfig!.StorageBucket,
            _readerConfig.Prefix,
            _readerConfig.Delimiter,
            _dataFilter.Body,
            _readerConfig.SkipEmptyObjects,
            _readerConfig.ReadFromRunStartTime
        );
        WaitUntilConsumptionTimeoutIsReached(timeout);
        IEnumerable<DetailedData<object>> s3ConsumedData;

        // If body is filtered dont query it in the first place
        if (!_dataFilter.Body)
        {
            // When the caller filters out the body, avoid downloading object contents at all and
            // return only ordering/timestamp metadata.
            s3ConsumedData = _s3Client!
                .ListAllObjectsInS3Bucket(
                    _readerConfig!.StorageBucket!,
                    _readerConfig.Prefix,
                    _readerConfig.Delimiter
                )
                .GetAwaiter()
                .GetResult()
                .Where(HasS3ObjectLastModified)
                .Where(IsS3ObjectRelevant)
                .OrderBy(GetS3ObjectTimestampUtc)
                .Select(s3Object => new DetailedData<object>
                {
                    Body = null,
                    Timestamp = GetS3ObjectTimestampUtc(s3Object),
                    MetaData = new MetaData { Storage = BuildStorageMetadata(s3Object) },
                });
        }
        else
        {
            // Preserve the raw bytes from S3 so downstream serializers/deserializers operate on the
            // exact payload that was stored in the bucket.
            s3ConsumedData = _s3Client!
                .GetAllObjectsInS3BucketUnOrdered(
                    _readerConfig.StorageBucket!,
                    _readerConfig.Prefix,
                    _readerConfig.Delimiter,
                    _readerConfig.SkipEmptyObjects
                )
                .Where(pair => HasS3ObjectLastModified(pair.Key))
                .Where(pair => IsS3ObjectRelevant(pair.Key))
                .OrderBy(pair => GetS3ObjectTimestampUtc(pair.Key))
                .Select(pair => new DetailedData<object>
                {
                    Body = pair.Value,
                    Timestamp = GetS3ObjectTimestampUtc(pair.Key),
                    MetaData = new MetaData { Storage = BuildStorageMetadata(pair.Key) },
                });
        }

        return s3ConsumedData;
    }

    public DetailedData<object> Send(Data<object> dataToSend)
    {
        // Resolve the key once so the upload path and the completion log refer to the same object name.
        var objectKey = dataToSend.MetaData?.Storage?.Key is { Length: > 0 } metadataKey
            ? _senderConfig!.Prefix + metadataKey
            : Generator.GenerateObjectName();
        _logger.LogDebug(
            "Uploading S3 object {ObjectKey} to bucket {BucketName}. Payload bytes: {PayloadLength}.",
            objectKey,
            _senderConfig!.StorageBucket,
            dataToSend.CastObjectData<byte[]>().Body?.Length ?? 0
        );
        S3Extentions.RunS3OperationWithRetryMechanism(
            () =>
            {
                using var memoryStream = new MemoryStream(
                    dataToSend.CastObjectData<byte[]>().Body ?? []
                ); // Assumes data is byte[]
                var putObjectRequest = new PutObjectRequest
                {
                    BucketName = _senderConfig!.StorageBucket,
                    Key = objectKey,
                    InputStream = memoryStream,
                    StorageClass = _senderConfig!.S3StorageClass.GetS3StorageClassFromEnum(),
                };

                AddStorageHeaders(putObjectRequest, dataToSend.MetaData?.Storage?.Headers);
                return _s3Client!.Client.PutObjectAsync(putObjectRequest).GetAwaiter().GetResult();
            },
            "uploading the object to s3",
            maxRetryCount: _senderConfig!.Retries,
            logger: _logger
        );
        _logger.LogInformation(
            "Finished uploading S3 object {ObjectKey} to bucket {BucketName}.",
            objectKey,
            _senderConfig.StorageBucket
        );
        return dataToSend.CloneDetailed();
    }

    private static void AddStorageHeaders(
        PutObjectRequest putObjectRequest,
        IDictionary<string, string>? headers
    )
    {
        if (headers is not { Count: > 0 })
            return;

        foreach (var (key, value) in headers)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            putObjectRequest.Metadata[key] = value;
        }
    }

    private bool HasS3ObjectLastModified(S3Object s3Object)
    {
        if (s3Object.LastModified.HasValue)
            return true;

        _logger.LogWarning(
            "Skipping S3 object {ObjectKey} from bucket {BucketName} because its LastModified value is missing.",
            s3Object.Key,
            _readerConfig!.StorageBucket
        );
        return false;
    }

    private Storage BuildStorageMetadata(S3Object s3Object) =>
        new()
        {
            Key = s3Object.Key,
            Headers = _readerConfig!.ReadStorageHeaders ? GetStorageHeaders(s3Object) : null,
        };

    private IDictionary<string, string>? GetStorageHeaders(S3Object s3Object)
    {
        if (string.IsNullOrWhiteSpace(s3Object.Key))
            return null;

        var response = S3Extentions.RunS3OperationWithRetryMechanism(
            () =>
                _s3Client!
                    .Client.GetObjectMetadataAsync(
                        _readerConfig!.StorageBucket!,
                        s3Object.Key,
                        null
                    )
                    .GetAwaiter()
                    .GetResult(),
            $"retrieving metadata for s3 object {s3Object.Key}",
            maxRetryCount: _readerConfig!.MaximumRetryCount,
            logger: _logger
        );
        if (response?.Metadata == null)
            return null;

        var headers = new Dictionary<string, string>();
        foreach (var key in response.Metadata.Keys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var value = response.Metadata[key];
            if (value is null)
                continue;

            headers[NormalizeStorageHeaderKey(key)] = value;
        }

        return headers.Count == 0 ? null : headers;
    }

    private static string NormalizeStorageHeaderKey(string key) =>
        key.StartsWith(S3UserMetadataPrefix, StringComparison.OrdinalIgnoreCase)
            ? key[S3UserMetadataPrefix.Length..]
            : key;

    private bool IsS3ObjectRelevant(S3Object s3Object)
    {
        if (!_readerConfig!.ReadFromRunStartTime)
            return true;

        return GetS3ObjectTimestampUtc(s3Object) >= _readStartTimeUtc;
    }

    private static DateTime GetS3ObjectTimestampUtc(S3Object s3Object) =>
        s3Object.LastModified.GetValueOrDefault().ToUniversalTime();

    /// <summary>
    /// Returns the number of milliseconds that have passed since the last s3 bucket object updated,
    /// if no s3 bucket object update was ever made or the latest s3 bucket object update
    /// time could not be found return null
    /// </summary>
    private long? GetNumberOfMilliSecondsPassedSinceLastS3ObjectModification()
    {
        var allS3ObjectsInBucket = _s3Client!
            .ListAllObjectsInS3Bucket(
                _readerConfig!.StorageBucket!,
                _readerConfig.Prefix,
                _readerConfig.Delimiter
            )
            .GetAwaiter()
            .GetResult()
            .Where(HasS3ObjectLastModified)
            .Where(IsS3ObjectRelevant)
            .ToArray();
        if (!allS3ObjectsInBucket.Any())
            return null;

        var latestModificationTime = allS3ObjectsInBucket.Max(s3Object =>
            s3Object.LastModified.GetValueOrDefault()
        );
        if (latestModificationTime.Kind != DateTimeKind.Unspecified)
            return (long)
                Math.Round(
                    (
                        GetCurrentDateTimeUtc() - latestModificationTime.ToUniversalTime()
                    ).TotalMilliseconds,
                    MidpointRounding.ToZero
                );

        // DateTimeKind not specified -> cannot convert to UTC and determine how many milliseconds passed
        _logger.LogCritical(
            "Latest modification time in S3 bucket {S3Bucket} had DateTimeKind.Unspecified. The reader cannot determine inactivity duration and will treat the timeout as elapsed.",
            _readerConfig.StorageBucket!
        );
        return null;
    }

    /// <summary>
    /// Waits until a period of time the length of the configured timeout has passed since the last object written to
    /// the bucket
    /// </summary>
    private void WaitUntilConsumptionTimeoutIsReached(TimeSpan timeout)
    {
        long? milliSecondsSinceLastS3ObjectModified = 0;
        var timeoutMs = (int)timeout.TotalMilliseconds;
        do
        {
            Thread.Sleep(timeoutMs - (int)milliSecondsSinceLastS3ObjectModified);
            milliSecondsSinceLastS3ObjectModified =
                GetNumberOfMilliSecondsPassedSinceLastS3ObjectModification();
            if (milliSecondsSinceLastS3ObjectModified == null)
            {
                _logger.LogWarning(
                    "Could not determine S3 inactivity duration for bucket {BucketName}. Treating timeout {TimeoutMs} ms as elapsed.",
                    _readerConfig!.StorageBucket,
                    timeoutMs
                );
                milliSecondsSinceLastS3ObjectModified = timeoutMs;
            }

            _logger.LogDebug(
                "S3 inactivity window for bucket {BucketName}: elapsed {ElapsedMilliseconds} ms, target {TimeoutMilliseconds} ms.",
                _readerConfig!.StorageBucket,
                milliSecondsSinceLastS3ObjectModified,
                timeoutMs
            );
        } while (milliSecondsSinceLastS3ObjectModified < timeoutMs);
    }

    public void Connect()
    {
        if (_senderConfig != null)
        {
            _logger.LogInformation(
                "Connecting S3 sender for bucket {BucketName} at {ServiceUrl}.",
                _senderConfig.StorageBucket,
                _senderConfig.ServiceURL
            );
            var s3Client = new AmazonS3Client(
                _senderConfig.AccessKey,
                _senderConfig.SecretKey,
                new AmazonS3Config
                {
                    ServiceURL = _senderConfig.ServiceURL,
                    ForcePathStyle = _senderConfig.ForcePathStyle,
                }
            );
            _s3Client = new S3Client(s3Client, _logger, _senderConfig.Retries);
        }

        if (_readerConfig != null)
        {
            _logger.LogInformation(
                "Connecting S3 reader for bucket {BucketName} at {ServiceUrl}.",
                _readerConfig.StorageBucket,
                _readerConfig.ServiceURL
            );
            _s3Client = new S3Client(
                new AmazonS3Client(
                    _readerConfig.AccessKey,
                    _readerConfig.SecretKey,
                    new AmazonS3Config
                    {
                        ServiceURL = _readerConfig.ServiceURL,
                        ForcePathStyle = _readerConfig.ForcePathStyle,
                    }
                ),
                _logger,
                _readerConfig.MaximumRetryCount
            );
        }
    }

    public void Disconnect() { }

    public void Dispose()
    {
        _logger.LogDebug("Disposing S3 protocol resources.");
        _s3Client?.Dispose();
    }
}
