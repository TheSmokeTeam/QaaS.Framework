using QaaS.Framework.SDK.Session.CommunicationDataObjects;

namespace QaaS.Framework.SDK.Session.SessionDataObjects;

/// <summary>
/// describes the result data of a QaaS session with a generic data type for input and output
/// </summary>
public record GenericSessionData<TInput, TOutput> : BaseSessionData
{
    /// <summary>
    /// The list of data produced from all input producing communication actions in this session
    /// </summary>
    public List<CommunicationData<TInput>>? Inputs { get; init; }
    
    /// <summary>
    /// The list of data produced from all output producing communication actions in this session
    /// </summary>
    public List<CommunicationData<TOutput>>? Outputs { get; init; }
}