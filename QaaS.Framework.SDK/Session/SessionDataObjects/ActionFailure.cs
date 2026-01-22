namespace QaaS.Framework.SDK.Session.SessionDataObjects;

/// <summary>
/// Describes an action failure
/// </summary>
public record ActionFailure
{
    /// <summary>
    /// The failed action name
    /// </summary>
    public string Name { get; init; }
    
    /// <summary>
    /// The failed action
    /// </summary>
    public string? Action { get; init; }

    /// <summary>
    /// The failed action type
    /// </summary>
    public string ActionType { get; init; }
    
    /// <summary>
    /// The failure reason
    /// </summary>
    public Reason Reason { get; init; }
}