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
        // Restart only the interval timer for the next iteration so derived policies can
        // keep their own stage/ramp state instead of being reinitialized on every run.
        _intervalTimer.Restart();
    }

    /// <summary>
    /// Waits until the current send window opens without pinning a CPU core in a pure busy loop.
    /// </summary>
    private void WaitForNextExecutionSlot()
    {
        var remainingMilliseconds = MessageIntervalMilliseconds - _intervalTimer.ElapsedMilliseconds;
        while (remainingMilliseconds > 0)
        {
            if (remainingMilliseconds > 1)
            {
                Thread.Sleep((int)Math.Floor(remainingMilliseconds));
            }
            else
            {
                Thread.SpinWait(20);
            }

            remainingMilliseconds = MessageIntervalMilliseconds - _intervalTimer.ElapsedMilliseconds;
        }
    }

    /// <summary>
    /// Takes the extra time it took to perform the policy and recalculates the new adjusted rate accordingly.
    /// </summary>
    protected virtual void AdjustRate()
        => MessageIntervalMilliseconds =
            (1000 - (_intervalTimer.ElapsedMilliseconds - MessageIntervalMilliseconds)) / MessagesPerSecond;
}
