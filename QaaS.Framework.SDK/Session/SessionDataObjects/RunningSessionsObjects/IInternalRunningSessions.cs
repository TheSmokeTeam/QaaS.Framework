namespace QaaS.Framework.SDK.Session.SessionDataObjects.RunningSessionsObjects;

/// <summary>
/// Interface for <see cref="IRunningSessions"/> with internal access to QaaS components
/// </summary>
public interface IInternalRunningSessions : IRunningSessions
{
    /// <summary>
    /// Dictionary containing all the running sessions
    /// </summary>
    public IDictionary<string, RunningSessionData<object, object>> RunningSessionsDict { get; set; }

}