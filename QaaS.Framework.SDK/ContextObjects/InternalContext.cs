using QaaS.Framework.SDK.ExecutionObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects.RunningSessionsObjects;

namespace QaaS.Framework.SDK.ContextObjects;

/// <summary>
/// Context internally used by QaaS components, contains additional properties only relevant to internal QaaS usage
/// not relevant to package users.
/// </summary>
public class InternalContext : Context
{
    /// <summary>
    /// Contains access to specific implementation of currently running sessions object 
    /// </summary>
    public IInternalRunningSessions InternalRunningSessions { get; init; } = null!;

    /// <inheritdoc />
    public override IRunningSessions CurrentRunningSessions
    {
        get => InternalRunningSessions;
        init => throw new NotSupportedException(
            $"{nameof(InternalContext)} does not support direct " +
            $"initialization of {nameof(CurrentRunningSessions)}");
    }
    
    public Dictionary<string, object?> InternalGlobalDict
    {
        get => GlobalDict;
        set => GlobalDict = value;
    } 

}