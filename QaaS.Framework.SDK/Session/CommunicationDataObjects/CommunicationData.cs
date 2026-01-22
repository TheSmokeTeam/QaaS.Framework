using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Framework.SDK.Session.CommunicationDataObjects;

/// <summary>
/// Describes the result of a communication action
/// </summary>
public record CommunicationData<TData> : BaseCommunicationData
{
    public IList<DetailedData<TData>> Data { get; init; } = null!;
}