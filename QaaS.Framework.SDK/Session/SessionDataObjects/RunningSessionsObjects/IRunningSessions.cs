namespace QaaS.Framework.SDK.Session.SessionDataObjects.RunningSessionsObjects;

/// <summary>
/// All the currently running sessions
/// </summary>
public interface IRunningSessions
{
    /// <summary>
    /// Return all currently running sessions
    /// </summary>
    /// <returns>List of the running <see cref="RunningSessionData{TInput,TOutput}"/></returns>
    public IList<RunningSessionData<object, object>> GetAllSessions();

    /// <summary>
    /// Get a running session by its name
    /// </summary>
    /// <param name="sessionName">The name of the session</param>
    /// <returns>The running <see cref="RunningSessionData{TInput,TOutput}"/></returns>
    public RunningSessionData<object, object> GetSessionByName(string sessionName);

}