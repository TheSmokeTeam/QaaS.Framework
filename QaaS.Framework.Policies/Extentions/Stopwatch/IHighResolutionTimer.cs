namespace QaaS.Framework.Policies.Extentions.Stopwatch;

/// <summary>
/// Optional fractional-millisecond clock. Existing <see cref="ITimer"/> implementations
/// remain supported; rate policies use this capability when available.
/// </summary>
public interface IHighResolutionTimer : ITimer
{
    /// <summary>Total elapsed milliseconds, including the fractional part.</summary>
    double ElapsedTotalMilliseconds { get; }
}
