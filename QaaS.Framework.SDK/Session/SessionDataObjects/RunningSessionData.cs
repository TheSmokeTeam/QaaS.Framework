using QaaS.Framework.SDK.Session.CommunicationDataObjects;

namespace QaaS.Framework.SDK.Session.SessionDataObjects;

/// <summary>
/// describe a session object which is currently running
/// </summary>
public record RunningSessionData<TInput, TOutput> 
{
    public List<RunningCommunicationData<TInput>>? Inputs { get; init; }

    public List<RunningCommunicationData<TOutput>>? Outputs { get; init; }
    
}