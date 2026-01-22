using QaaS.Framework.SDK.ExecutionObjects;

namespace QaaS.Framework.Executions.Logics;

/// <summary>
/// Interface for implementations of internal executions and logics of main QaaS flow
/// </summary>
public interface ILogic
{
    /// <summary>
    /// Dictates whether to run or skip the logic by the QaaS <see cref="ExecutionType"/> Type.
    /// </summary>
    public bool ShouldRun(ExecutionType executionType);

    /// <summary>
    /// The main-code logic to run under this logic's responsibilities that modifies the <see cref="ExecutionData"/> runData.
    /// </summary>
    public ExecutionData Run(ExecutionData executionData);
}