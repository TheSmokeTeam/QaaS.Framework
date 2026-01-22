namespace QaaS.Framework.Policies.Extentions.Stopwatch;

/// <summary>
/// Interface of a timer used to measure time intervals.
/// </summary>
public interface ITimer
{
    /// <summary>
    /// Stops time interval measurement, resets the elapsed time to zero, and starts measuring elapsed time
    /// </summary>
    void Restart();

    /// <summary>
    /// Total elapsed milliseconds since start.
    /// </summary>
    public long ElapsedMilliseconds { get; }
}