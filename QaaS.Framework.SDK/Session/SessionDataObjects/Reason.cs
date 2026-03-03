namespace QaaS.Framework.SDK.Session.SessionDataObjects;

/// <summary>
/// Represent the failure reason
/// </summary>
public record Reason
{
    /// <summary>
    /// A short message about the failure reason
    /// </summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>
    /// A detailed description of the failure reason
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
