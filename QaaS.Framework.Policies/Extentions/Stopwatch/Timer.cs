using System.Diagnostics;

namespace QaaS.Framework.Policies.Extentions.Stopwatch;

/// <summary>
/// Implementation of <see cref="ITimer"/> using <see cref="Stopwatch"/>.
/// </summary>
public class Timer : IHighResolutionTimer
{
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();

    /// <inheritdoc />
    public void Restart() => _stopwatch.Restart();

    /// <inheritdoc />
    public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;

    /// <inheritdoc />
    public double ElapsedTotalMilliseconds => _stopwatch.Elapsed.TotalMilliseconds;
}
