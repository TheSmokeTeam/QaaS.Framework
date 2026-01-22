namespace QaaS.Framework.SDK.Hooks.Assertion;

/// <summary>
/// Represents the status of an assertion after it was executed
/// </summary>
public enum AssertionStatus
{
    /// <summary>
    /// Assertion passed
    /// </summary>
    Passed,
    /// <summary>
    /// Assertion failed
    /// </summary>
    Failed,
    /// <summary>
    /// Assertion raised some sort of exception
    /// </summary>
    Broken,
    /// <summary>
    /// Represents assertion that its result is unknown
    /// </summary>
    Unknown,
    /// <summary>
    /// Represents assertion that was skipped
    /// </summary>
    Skipped
}