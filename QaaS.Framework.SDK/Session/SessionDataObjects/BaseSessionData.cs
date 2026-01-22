namespace QaaS.Framework.SDK.Session.SessionDataObjects;

/// <summary>
/// Base record that contains all bare minimum items to describe the result data of a communication session 
/// </summary>
public abstract record BaseSessionData
{
    /// <summary>
    /// The name of the session that produces this session data
    /// </summary>
    public string Name { get; init; } = null!;
    
    /// <summary>
    /// The UTC start time of the session
    /// </summary>
    public DateTime UtcStartTime { get; init; }
    
    /// <summary>
    /// The UTC end time of the session
    /// </summary>
    public DateTime UtcEndTime { get; init; }
     
    /// <summary>
    /// The failures encountered while the session ran
    /// </summary>
    public List<ActionFailure> SessionFailures { get; init; }
}
 