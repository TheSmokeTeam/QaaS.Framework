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
        ApplyStage(_currStage);
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
        var stage = _stages[_currStage];
        var hasAmountLimit = stage.AmountToNextStage != null;
        var hasTimeLimit = stage.TimeToNextStage != null;

        if (!hasAmountLimit && !hasTimeLimit)
            throw new InvalidOperationException(
                "Exception: You must set 'Amount To Next Stage' or 'Time To Next Stage' in the AdvancedLoadBalance stages.");

        if (hasAmountLimit && stage.AmountToNextStage <= _amountPassed)
            return true;

        if (hasTimeLimit && stage.TimeToNextStage <= _timePassed.Elapsed.TotalMilliseconds)
            return true;

        return false;
    }

    protected override void RunThis()
    {
        _amountPassed++;
        if (IsEndOfStage())
        {
            if (_currStage < _stages.Length - 1)
                _currStage++;

            ApplyStage(_currStage);
            ResetStage();
        }

        base.RunThis();
    }

    private void ApplyStage(int stageIndex)
    {
        MessagesPerSecond = _stages[stageIndex].MessagesPerSecond;
        MessageIntervalMilliseconds = 1000 / MessagesPerSecond;
    }
}
