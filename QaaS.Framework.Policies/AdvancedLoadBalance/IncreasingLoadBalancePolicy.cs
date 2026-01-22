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
        MessageIntervalMilliseconds = 1000 / (rate / intervalMs);

        _rateIncreaseMessagesPerSecond = rateIncreaseMessagesPerSecond;
        _rateIncreaseIntervalMs = rateIncreaseIntervalMs;

        _maxMsgPerSec = maxRate;
    }

    protected override void SetupThis()
    {
        base.SetupThis();
        _increaseTimer.Restart();
    }

    protected override void RunThis()
    {
        if (MessagesPerSecond < _maxMsgPerSec && _increaseTimer.Elapsed.TotalMilliseconds < _rateIncreaseIntervalMs)
        {
            MessagesPerSecond += _rateIncreaseMessagesPerSecond;
            MessageIntervalMilliseconds = 1000 / (double)MessagesPerSecond;
        }

        base.RunThis();
    }
}