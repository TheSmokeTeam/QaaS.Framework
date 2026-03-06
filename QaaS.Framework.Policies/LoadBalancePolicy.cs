using ITimer = QaaS.Framework.Policies.Extentions.Stopwatch.ITimer;
using Timer = QaaS.Framework.Policies.Extentions.Stopwatch.Timer;

namespace QaaS.Framework.Policies;

public class LoadBalancePolicy : Policy
{
    private readonly ITimer _intervalTimer;
    protected double MessageIntervalMilliseconds;
    protected double MessagesPerSecond;
    protected override uint Index { get; set; } = int.MaxValue;

    public LoadBalancePolicy(double rate, ulong intervalMs, ITimer? timer = null)
    {
        MessagesPerSecond = rate / intervalMs * 1000;
        MessageIntervalMilliseconds = 1000 / MessagesPerSecond;
        _intervalTimer = timer ?? new Timer();
    }

    protected override void SetupThis()
    {
        _intervalTimer.Restart();
    }

    protected override void RunThis()
    {
        WaitForNextExecutionSlot();

        AdjustRate();
        _intervalTimer.Restart();
    }

    private void WaitForNextExecutionSlot()
    {
        while (true)
        {
            var remainingMilliseconds = MessageIntervalMilliseconds - _intervalTimer.ElapsedMilliseconds;
            if (remainingMilliseconds <= 0)
                return;

            if (remainingMilliseconds > 1)
            {
                Thread.Sleep((int)Math.Floor(remainingMilliseconds));
                continue;
            }

            Thread.SpinWait(20);
        }
    }

    /// <summary>
    /// Takes the extra time it took to perform the policy and recalculates the new adjusted rate accordingly.
    /// </summary>
    protected virtual void AdjustRate()
        => MessageIntervalMilliseconds =
            (1000 - (_intervalTimer.ElapsedMilliseconds - MessageIntervalMilliseconds)) / MessagesPerSecond;
}
