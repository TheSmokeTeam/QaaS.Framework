using QaaS.Framework.SDK.ExecutionObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects.RunningSessionsObjects;

namespace QaaS.Framework.SDK.ContextObjects;

/// <summary>
/// Contains QaaS run's relevant context objects such as Logger and Configuration.
/// </summary>
public class Context : BaseContext<ExecutionData>
{
    /// <summary>
    /// The name of the case this context was created for, if null the current execution does not have cases.
    /// </summary>
    public string? CaseName { get; init; }

    /// <summary>
    /// The id of the execution this context was created in, if null the current run only executes 1 commands.
    /// </summary>
    public string? ExecutionId { get; init; }

    /// <summary>
    /// Contains all the currently running sessions 
    /// </summary>
    public virtual IRunningSessions CurrentRunningSessions { get; init; } = null!;

}