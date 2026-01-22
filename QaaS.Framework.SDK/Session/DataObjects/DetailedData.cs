using QaaS.Framework.Serialization.Serializers;

namespace QaaS.Framework.SDK.Session.DataObjects;

/// <summary>
/// Extends Data with a timestamp field of when that data was last interacted with.
/// </summary>
/// <typeparam name="T"> Type of data body </typeparam>
public record DetailedData<T>: Data<T>
{
    /// <summary>
    /// The UTC time this data was first interacted with by a communication action
    /// </summary>
    public DateTime? Timestamp { get; init; }
}
