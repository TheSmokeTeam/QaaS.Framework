using System.Diagnostics;

namespace QaaS.Framework.Policies.AdvancedLoadBalance;

public class IncreasingLoadBalancePolicy : LoadBalancePolicy
{
    private readonly ulong _rateIncreaseMessagesPerSecond;
    private readonly ulong _maxMsgPerSec;
    private readonly double _rateIncreaseIntervalMs;
    private readonly Stopwatch _increaseTimer = new();

    public IncreasingLoadBalancePolicy(double rate, ulong intervalMs, ulong maxRate,
        ulong rateIncreaseMessagesPerSecond, double rateIncreaseIntervalMs)
        : base(rate, intervalMs)
    {
        _rateIncreaseMessagesPerSecond = rateIncreaseMessagesPerSecond;
        _rateIncreaseIntervalMs = rateIncreaseIntervalMs;
        _maxMsgPerSec = maxRate;
    }

    protected override void SetupThis()
    {
        base.SetupThis();
        // The ramp timer measures the elapsed time across runs, so start it once and only
        // restart it when a rate increase actually happens.
        if (!_increaseTimer.IsRunning)
            _increaseTimer.Restart();
    }

    protected override void RunThis()
    {
        // Only increase the rate after the configured interval has elapsed; otherwise the
        // previous implementation could ramp immediately on every execution.
        if (MessagesPerSecond < _maxMsgPerSec && _increaseTimer.Elapsed.TotalMilliseconds >= _rateIncreaseIntervalMs)
        {
            MessagesPerSecond = Math.Min(MessagesPerSecond + _rateIncreaseMessagesPerSecond, _maxMsgPerSec);
            MessageIntervalMilliseconds = 1000 / (double)MessagesPerSecond;
            _increaseTimer.Restart();
        }

        base.RunThis();
    }
}
