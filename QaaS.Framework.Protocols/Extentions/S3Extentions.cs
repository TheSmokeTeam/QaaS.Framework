using Amazon.S3;
using Microsoft.Extensions.Logging;

namespace QaaS.Framework.Protocols.Extentions;

/// <summary>
/// Extensions to the available operations on s3
/// </summary>
public static class S3Extentions
{
    /// <summary>
    /// Runs any s3 operation with a retry mechanism that handles the too many requests exception 
    /// </summary>
    /// <param name="s3OperationWithParameters"> A function that performs an s3 operation and returns
    /// the result of the operation</param>
    /// <param name="operationDescription"> The description of the operation,
    /// used for a log incase the operation does need a retry</param>
    /// <param name="maxRetryCount"> The maximum amount of retries when not successful,
    /// if no value is given there is no maximum amount and it can retry forever </param>
    /// <param name="logger"> The logger to log with </param>
    /// <typeparam name="T"> The return type of the given function </typeparam>
    /// <returns> The return value the given function returns </returns>
    public static T RunS3OperationWithRetryMechanism<T>(Func<T> s3OperationWithParameters,
        string operationDescription, int? maxRetryCount = null, ILogger? logger = null)
    {
        bool retryIfTooManyRequestsWereSentTooFast;
        var retryCount = 0;
        T result = default;
        do
        {
            retryIfTooManyRequestsWereSentTooFast = false;
            try
            {
                result = s3OperationWithParameters.Invoke();
            }
            catch (AmazonS3Exception ex)
            {
                // If error is not the too many requests error or the retry count has reached its limit, throw exception
                if (ex.ErrorCode != "TooManyRequests" || retryCount++ >= maxRetryCount)
                    throw;
                retryIfTooManyRequestsWereSentTooFast = true;
                logger?.LogDebug("Encountered `TooManyRequests` exception when performing the operation:" +
                                 " {OperationDescription} on s3, retrying...", operationDescription);
            }
        } while (retryIfTooManyRequestsWereSentTooFast);

        return result;
    }
}