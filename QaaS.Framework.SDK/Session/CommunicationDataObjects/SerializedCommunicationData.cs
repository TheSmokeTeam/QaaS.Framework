using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Framework.SDK.Session.CommunicationDataObjects;

/// <summary>
/// The serialized appearance of the CommunicationData record
/// </summary>
public record SerializedCommunicationData: BaseCommunicationData
{
    /// <summary>
    /// The serialized data produced as a result of the communication action
    /// </summary>
    public List<SerializedDetailedData> Data { get; init; } = null!;
}