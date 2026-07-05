namespace QaaS.Framework.Serialization;

/// <summary>
/// Indicative exception thrown when a QaaS serialization or deserialization operation fails.
/// The message always describes what was being processed, with which serialization format and why it failed,
/// while the original failure (if any) is preserved as the InnerException
/// </summary>
/// <remarks>
/// Example: `try { QaasSerializer.Deserialize&lt;Order&gt;(bytes, SerializationType.Json); } catch (QaasSerializationException e) { logger.Error(e.Message); }`
/// </remarks>
public class QaasSerializationException : Exception
{
    /// <summary>
    /// Creates a new QaasSerializationException with an indicative message
    /// </summary>
    /// <param name="message"> The indicative message describing the serialization failure </param>
    public QaasSerializationException(string message)
        : base(message) { }

    /// <summary>
    /// Creates a new QaasSerializationException with an indicative message and the original failure
    /// </summary>
    /// <param name="message"> The indicative message describing the serialization failure </param>
    /// <param name="innerException"> The original exception that caused the failure </param>
    public QaasSerializationException(string message, Exception? innerException)
        : base(message, innerException) { }
}
