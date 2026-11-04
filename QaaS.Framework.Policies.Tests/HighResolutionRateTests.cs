using QaaS.Framework.Policies.Extentions.Stopwatch;

namespace QaaS.Framework.Policies.Tests;

[TestFixture]
public class HighResolutionRateTests
{
    private sealed class Clock : IHighResolutionTimer
    {
        public int Reads { get; private set; }
        public int Restarts { get; private set; }

        public void Restart() => Restarts++;

        public long ElapsedMilliseconds =>
            throw new AssertionException("Rate policy truncated elapsed time.");
        public double ElapsedTotalMilliseconds => ++Reads * 0.025;
    }

    [Test]
    public void TenThousandPerSecond_UsesFractionalMilliseconds()
    {
        var clock = new Clock();
        var policy = new LoadBalancePolicy(10000, 1000, clock);
        policy.SetupChain();
        Assert.That(policy.RunChain(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(clock.Reads, Is.InRange(4, 6));
            Assert.That(clock.Restarts, Is.EqualTo(2));
        });
    }
}
