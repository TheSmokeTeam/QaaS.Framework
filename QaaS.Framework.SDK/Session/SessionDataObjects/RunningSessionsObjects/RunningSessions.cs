namespace QaaS.Framework.SDK.Session.SessionDataObjects.RunningSessionsObjects;

/// <inheritdoc />
public class RunningSessions(IDictionary<string, RunningSessionData<object, object>> runningSessionsDict)
    : IInternalRunningSessions
{
    /// <inheritdoc />
    public IDictionary<string, RunningSessionData<object, object>> RunningSessionsDict { get; set; } =
        runningSessionsDict;

    /// <inheritdoc />
    public IList<RunningSessionData<object, object>> GetAllSessions() =>
        RunningSessionsDict.Values.ToList();

    /// <inheritdoc />
    public RunningSessionData<object, object> GetSessionByName(string sessionName)
    {
        if (!RunningSessionsDict.TryGetValue(sessionName, out var runningSession))
            throw new ArgumentException($"Session {sessionName} not found in current running sessions");
        return runningSession;
    }
}