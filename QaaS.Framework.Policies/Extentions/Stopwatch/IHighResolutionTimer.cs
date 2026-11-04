namespace QaaS.Framework.Policies.Extentions.Stopwatch;

/// <summary>
/// Fractional-millisecond clock capability used by rate policies when available.
/// Timers implementing only <see cref="ITimer"/> supply whole elapsed milliseconds.
/// </summary>
public interface IHighResolutionTimer : ITimer
{
    /// <summary>Total elapsed milliseconds, including the fractional part.</summary>
    double ElapsedTotalMilliseconds { get; }
}
