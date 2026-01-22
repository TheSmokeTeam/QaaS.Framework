using Amazon.S3;
using Amazon.S3.Model;

namespace QaaS.Framework.Protocols.Utils.S3Utils;

/// <summary>
/// Interface for all s3 data manipulation with a single client related functionality
/// </summary>
public interface IS3Client : IDisposable
{
    /// <summary>
    /// The AmasonS3 object to wrap on the client-extended QaaS-based functionalities.
    /// </summary>
    public IAmazonS3 Client { get; init; }

    /// <summary>
    /// Deletes all contents of a s3 bucket
    /// </summary>
    /// <param name="bucketName"> The name of the s3 bucket to empty </param>
    /// <param name="prefix"> Prefix of all objects to delete </param>
    /// <param name="delimiter">Delimiter to use between object names to use when deleting,
    /// objects that don't have the delimiter in their path in the bucket will be deleted</param>
    /// <returns> Delete responses of all the delete requests performed by the function (each delete request
    /// can only delete up to 1000 objects so multiple responses will be returned if there are more than
    /// 1000 objects in the bucket) </returns>
    public Task<IEnumerable<DeleteObjectsResponse>> EmptyS3Bucket(string bucketName,
        string prefix = "", string delimiter = "");

    /// <summary>
    /// Lists all the objects with the given prefix in the given bucket
    /// </summary>
    /// <param name="bucketName"> The name of the s3 bucket to list the objects in</param>
    /// <param name="prefix"> prefix of all objects to list </param>
    /// <param name="delimiter">delimiter of the message keys</param>
    /// <param name="skipEmptyObjects"> Whether to skip found empty s3Objects or not, by default is true which
    /// means it skips them, if false will simply save their stream as null </param>
    /// <returns>an enumerable of all the objects with the given prefix in the given bucket</returns>
    public Task<IEnumerable<S3Object>> ListAllObjectsInS3Bucket(string bucketName,
        string prefix = "", string delimiter = "", bool skipEmptyObjects = true);

    /// <summary>
    /// Retrieves an enumerable of all objects with a given prefix in the given bucket in no particular order that contains a
    /// key value pair where the key is the S3Object (which contains all of its metadata) and the value is
    /// the s3Object's data accessible through a stream (memory stream is used).
    /// </summary>
    /// <param name="bucketName"> The name of the bucket the objects are in </param>
    /// <param name="prefix"> The prefix of all the desired objects </param>
    /// <param name="delimiter">delimiter of the message keys</param>
    /// <param name="skipEmptyObjects"> Whether to skip found empty s3Objects or not, by default is true which
    /// means it skips them, if false will simply save their stream as null </param>
    /// <returns> All objects metadata as key and their data in byte[] form as value in a key value pair enumerable
    /// </returns>
    public IEnumerable<KeyValuePair<S3Object, byte[]?>> GetAllObjectsInS3BucketUnOrdered(
        string bucketName, string prefix = "", string delimiter = "", bool skipEmptyObjects = true);

    /// <summary>
    /// Retrieves an object in the given bucket
    /// key value pair where the key is the S3Object (which contains all the object's metadata) and the value is
    /// the s3Object's data
    /// </summary>
    /// <param name="s3ObjectMetadata">Metadata of each object to load</param>
    /// <param name="bucketName"> The name of the bucket the objects are in </param>
    /// <returns> All objects metadata as key and their data in byte[] form as value in a key value pair enumerable
    /// </returns>
    public KeyValuePair<S3Object, byte[]?> GetObjectFromObjectMetadata(
        S3Object s3ObjectMetadata, string bucketName);

    /// <summary>
    /// Puts the given items by their order from first to last synchronously and lazily in an s3 bucket where the key is the
    /// key of the item and the value is the item's value serialized to byte[]
    /// </summary>
    /// <param name="bucketName"> The name of the s3 bucket to store the items in, if doesn't exist creates it. </param>
    /// <param name="s3KeyValueItems"> The items to put in the s3 bucket, the key is the item's key and its value is its serialized value</param>
    /// <returns> Returns an enumerable of the responses from putting every item in the s3 bucket </returns>
    /// <exception cref="HttpRequestException"> Thrown when a response does not contain a valid http status code </exception>
    public IEnumerable<PutObjectResponse> PutObjectsInS3BucketSync(string bucketName,
        IEnumerable<KeyValuePair<string, byte[]>> s3KeyValueItems);
}