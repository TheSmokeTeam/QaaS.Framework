using QaaS.Framework.SDK.Session.CommunicationDataObjects;

namespace QaaS.Framework.SDK.Session.SessionDataObjects;

/// <summary>
/// The serialized appearance of the SessionData record
/// </summary>
public record SerializedSessionData : BaseSessionData
{
    /// <summary>
    /// The list of serialized data produced from all input producing communication actions in this session
    /// </summary>
    public List<SerializedCommunicationData>? Inputs { get; init; }
    
    /// <summary>
    /// The list of serialized data produced from all output producing communication actions in this session
    /// </summary>
    public List<SerializedCommunicationData>? Outputs { get; init; } 
}