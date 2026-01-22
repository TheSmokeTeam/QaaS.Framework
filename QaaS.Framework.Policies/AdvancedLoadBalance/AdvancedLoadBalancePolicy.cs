using System.Diagnostics;
using QaaS.Framework.Policies.ConfigurationObjects;

namespace QaaS.Framework.Policies.AdvancedLoadBalance;

public class AdvancedLoadBalancePolicy : LoadBalancePolicy
{
    private int _currStage;
    private ulong _amountPassed;
    private readonly Stopwatch _timePassed = new();
    private readonly LoadBalanceStage[] _stages;

    public AdvancedLoadBalancePolicy(StageConfig[] stages) : base(stages[0].Rate!.Value, stages[0].TimeIntervalMs)
    {
        _stages = stages
            .Select(config => new LoadBalanceStage(
                rate: config.Rate!.Value, intervalMs: config!.TimeIntervalMs,
                config.Amount, config.TimeoutMs)).ToArray();
    }

    protected override void SetupThis()
    {
        _currStage = 0;
        ResetStage();
        base.SetupThis();
    }

    private void ResetStage()
    {
        _amountPassed = 0;
        _timePassed.Restart();
    }

    private bool IsEndOfStage()
    {
        if (_stages[_currStage].AmountToNextStage != null &&
            _stages[_currStage].AmountToNextStage <= _amountPassed)
            return true;

        if (_stages[_currStage].TimeToNextStage != null &&
            _stages[_currStage].TimeToNextStage <= _timePassed.Elapsed.TotalMilliseconds)
            return true;

        throw new InvalidOperationException(
            "Exception: You must set 'Amount To Next Stage' or 'Time To Next Stage' in the AdvancedLoadBalance stages.");
    }

    protected override void RunThis()
    {
        _amountPassed++;
        if (IsEndOfStage())
        {
            _currStage++;
            ResetStage();
            MessageIntervalMilliseconds = 1000 / (double)_stages[_currStage].MessagesPerSecond;
        }

        base.RunThis();
    }
}