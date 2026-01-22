using System.Diagnostics;
using QaaS.Framework.Policies.Exceptions;

namespace QaaS.Framework.Policies;

public class TimeoutPolicy : Policy
{
    private readonly Stopwatch _stopwatch = new();
    private readonly TimeSpan _timeout;
    
    protected override uint Index { get; set; } = 0;
    
    public TimeoutPolicy(uint timeOutMs) => _timeout = TimeSpan.FromMilliseconds(timeOutMs);
    
    protected override void SetupThis() => _stopwatch.Restart();

    protected override void RunThis()
    {
        if (_timeout <= _stopwatch.Elapsed)
            throw new TimeoutStopException(_timeout);
    }
}