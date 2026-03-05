using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Protocols.Extentions;

namespace QaaS.Framework.Protocols.Utils.S3Utils;

/// <summary>
/// Contains functionality for manipulating s3 data for a single client
/// </summary>
[ExcludeFromCodeCoverage]
public class S3Client : IS3Client
{
    private readonly ILogger? _logger;
    private readonly int? _maxRetryCount;
    public IAmazonS3 Client { get; init; }

    /// <summary>
    /// Constructor of S3SingleClientDataManipulator
    /// </summary>
    /// <param name="client"> S3 client to connect to for all functionality related to a single s3 client </param>
    /// <param name="logger"> Logger to use in functions, by default null and won't log </param>
    /// <param name="maxRetryCount"> Maximum retry count for all operations against s3 if an known
    /// exception is encountered </param>
    public S3Client(IAmazonS3 client, ILogger? logger = null, int? maxRetryCount = null)
    {
        Client = client;
        _logger = logger;
        _maxRetryCount = maxRetryCount;
    }


    /// <inheritdoc />
    public async Task<IEnumerable<DeleteObjectsResponse>> EmptyS3Bucket(string bucketName,
        string prefix = "", string delimiter = "")
    {
        // Request for getting objects to delete
        var listObjectsV2Request = new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = prefix,
            Delimiter = delimiter
        };

        // List of all deletion's responses 
        var deletionResponse = new List<DeleteObjectsResponse>();

        var listObjectsV2Response = new ListObjectsV2Response { IsTruncated = true };
        do
        {
            await S3Extentions.RunS3OperationWithRetryMechanism(async () =>
                {
                    listObjectsV2Response = await Client.ListObjectsV2Async(listObjectsV2Request);

                    // Delete all objects found in the latest request and keep the deletion response 

                    var keysOfAllS3BucketContents = listObjectsV2Response.S3Objects
                        .Select(item => new KeyVersion { Key = item.Key }).ToList();

                    var s3BucketAllObjectDeletionRequest = new DeleteObjectsRequest
                    {
                        BucketName = bucketName,
                        Objects = keysOfAllS3BucketContents
                    };

                    deletionResponse.Add(await Client.DeleteObjectsAsync(s3BucketAllObjectDeletionRequest));

                    // Continue from where it stopped listing objects
                    listObjectsV2Request.ContinuationToken = listObjectsV2Response.NextContinuationToken;
                }, $"List and then delete a chunk of objects in s3 bucket {bucketName} with" +
                   $" prefix {prefix}", logger: _logger, maxRetryCount: _maxRetryCount);
        } while (listObjectsV2Response.IsTruncated.Value);

        return deletionResponse;
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<S3Object>> ListAllObjectsInS3Bucket(string bucketName,
        string prefix = "", string delimiter = "", bool skipEmptyObjects = true)
    {
        var listOfObjects = new List<S3Object>();
        var request = new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = prefix,
            Delimiter = delimiter
        };

        var response = new ListObjectsV2Response { IsTruncated = true };
        do
        {
            await S3Extentions.RunS3OperationWithRetryMechanism(async () =>
                {
                    response = await Client.ListObjectsV2Async(request);
                    listOfObjects.AddRange(response.S3Objects);

                    // Continue from where it stopped listing objects
                    request.ContinuationToken = response.NextContinuationToken;
                }, $"List a chunk of objects in s3 bucket {bucketName} with prefix {prefix} and delimiter {delimiter}",
                logger: _logger, maxRetryCount: _maxRetryCount);
        } while (response.IsTruncated.Value);

        return skipEmptyObjects
            ? listOfObjects.Where(obj => obj.Size > 0).ToList()
            : listOfObjects;
    }

    /// <inheritdoc />
    public KeyValuePair<S3Object, byte[]?> GetObjectFromObjectMetadata(
        S3Object s3ObjectMetadata, string bucketName)
    {
        var retrievedObject = new KeyValuePair<S3Object, byte[]?>();
        S3Extentions.RunS3OperationWithRetryMechanism(() =>
            {
                using var response = Client.GetObjectAsync(bucketName, s3ObjectMetadata.Key, null);
                using var responseStream = response.Result.ResponseStream;
                using var streamReader = new StreamReader(responseStream);
                var retrievedObjectFromS3 = System.Text.Encoding.UTF8.GetBytes(streamReader.ReadToEnd());
                retrievedObject = new KeyValuePair<S3Object, byte[]?>(s3ObjectMetadata, retrievedObjectFromS3);
                return retrievedObjectFromS3;
            }, $"Retrieved s3 object {s3ObjectMetadata.Key} in bucket {bucketName}",
            logger: _logger, maxRetryCount: _maxRetryCount);

        return retrievedObject;
    }

    /// <inheritdoc />
    public IEnumerable<KeyValuePair<S3Object, byte[]?>> GetAllObjectsInS3BucketUnOrdered(
        string bucketName, string prefix = "", string delimiter = "", bool skipEmptyObjects = true)
    {
        var s3Objects = ListAllObjectsInS3Bucket(
            bucketName, prefix, delimiter, skipEmptyObjects).Result.ToList();
        _logger?.LogDebug("Found {NumberOfS3Objects} objects in the bucket {S3Bucket} with prefix" +
                          " {S3Prefix}", s3Objects.Count, bucketName, prefix);

        var s3ObjectsStreamPairs = new ConcurrentBag<KeyValuePair<S3Object, byte[]?>>();
        Parallel.ForEach(s3Objects,
            s3Object =>
            {
                switch (s3Object.Size <= 0)
                {
                    case true:
                        // Ignore empty files/folders
                        if (skipEmptyObjects)
                            return;

                        // Don't ignore empty files/folders
                        _logger?.LogDebug("Added empty s3Object {S3ObjectKey} with null stream", s3Object.Key);
                        s3ObjectsStreamPairs.Add(new KeyValuePair<S3Object, byte[]?>(s3Object, null));
                        return;

                    // Not an empty file/folder
                    default:
                        S3Extentions.RunS3OperationWithRetryMechanism(() =>
                            {
                                var objectStream = Client.GetObjectStreamAsync(
                                    bucketName, s3Object.Key,
                                    null).Result;

                                // Move from objectStream to memory stream so the socket and all s3 related resources can be disposed
                                MemoryStream memoryStream;
                                // try-catch meant to catch a case when the stream has no length - in that case the 
                                // stream capacity will be dynamic
                                try
                                {
                                    memoryStream = new MemoryStream((int)objectStream.Length);
                                }
                                catch (Exception)
                                {
                                    memoryStream = new MemoryStream();
                                }

                                if (!objectStream.CanRead)
                                {
                                    _logger?.LogError("Could not read object stream of object {S3ObjectKey}," +
                                                      " Skipped loading that object",
                                        s3Object.Key);
                                    return null;
                                }

                                objectStream.CopyTo(memoryStream);
                                objectStream.Dispose();

                                s3ObjectsStreamPairs.Add(new KeyValuePair<S3Object, byte[]?>(s3Object,
                                    memoryStream.ToArray()));
                                memoryStream.Dispose();
                                return memoryStream;
                            }, $"Retrieve stream of s3 object {s3Object.Key} in bucket {bucketName}",
                            logger: _logger, maxRetryCount: _maxRetryCount);
                        break;
                }
            });
        _logger?.LogInformation("Retrieving streams from s3, {s3ObjectsCount} objects" +
                                " found at given `bucket:path` - `{S3Bucket}:{S3Prefix}` with delimiter {delimiter}. Actual number of " +
                                "objects retrieved after filtering {FilteredObjects}" +
                                " objects was {S3ObjectsStreamPairsCount}",
            s3Objects.Count, bucketName, prefix, delimiter, skipEmptyObjects ? "empty/defected" : "defected",
            s3ObjectsStreamPairs.Count);

        return s3ObjectsStreamPairs;
    }

    /// <inheritdoc />
    public IEnumerable<PutObjectResponse> PutObjectsInS3BucketSync(string bucketName,
        IEnumerable<KeyValuePair<string, byte[]>> s3KeyValueItems)
    {
        // If bucket doesn't exist create it
        try
        {
            Client.EnsureBucketExistsAsync(bucketName).GetAwaiter().GetResult();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            Client.PutBucketAsync(bucketName).Wait();
        }

        var storedItemsCounter = 0;
        foreach (var pair in s3KeyValueItems)
        {
            yield return S3Extentions.RunS3OperationWithRetryMechanism(() =>
                {
                    using var memoryStream = new MemoryStream(pair.Value);
                    memoryStream.Position = 0;
                    var response = Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = bucketName,
                        Key = pair.Key,
                        InputStream = memoryStream,
                        StorageClass = S3StorageClass.StandardInfrequentAccess
                    }).Result;
                    if (response.HttpStatusCode != HttpStatusCode.OK)
                        throw new HttpRequestException(
                            $"Got {response.HttpStatusCode} http status code response when " +
                            $"trying to save data to s3 bucket {bucketName} at path {pair.Key}");
                    return response;
                }, $"uploading the object {pair.Key} to bucket {bucketName}", _maxRetryCount, _logger);
            storedItemsCounter++;
        }

        _logger?.LogInformation("Stored {NumberOfStoredItems} items in s3 bucket {BucketName}",
            storedItemsCounter, bucketName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Client.Dispose();
    }
}

