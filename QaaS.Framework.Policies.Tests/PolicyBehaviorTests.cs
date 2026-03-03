using System.Reflection;
using QaaS.Framework.Policies.AdvancedLoadBalance;
using QaaS.Framework.Policies.ConfigurationObjects;
using QaaS.Framework.Policies.Exceptions;

namespace QaaS.Framework.Policies.Tests;

[TestFixture]
public class PolicyBehaviorTests
{
    private sealed class TriggerStopPolicy : Policy
    {
        protected override uint Index { get; set; }

        protected override void SetupThis()
        {
        }

        protected override void RunThis()
        {
            throw new StopActionException("stop");
        }
    }

    [Test]
    public void CountPolicy_RunChain_StopsAfterConfiguredCount()
    {
        var policy = new CountPolicy(2);
        policy.SetupChain();

        var firstRun = policy.RunChain();
        var secondRun = policy.RunChain();

        Assert.That(firstRun, Is.True);
        Assert.That(secondRun, Is.False);
    }

    [Test]
    public void TimeoutPolicy_RunChain_StopsAfterTimeout()
    {
        var policy = new TimeoutPolicy(1);
        policy.SetupChain();
        Thread.Sleep(5);

        var shouldContinue = policy.RunChain();

        Assert.That(shouldContinue, Is.False);
    }

    [Test]
    public void Policy_RunChain_ReturnsFalse_WhenStopActionExceptionIsThrown()
    {
        var policy = new TriggerStopPolicy();

        var result = policy.RunChain();

        Assert.That(result, Is.False);
    }

    [Test]
    public void LoadBalanceStage_ComputesMessagesPerSecond()
    {
        var stage = new LoadBalanceStage(rate: 10, intervalMs: 1000, amountToNextStage: 5, timeToNextStage: null);

        Assert.That(stage.MessagesPerSecond, Is.EqualTo(10));
        Assert.That(stage.AmountToNextStage, Is.EqualTo(5));
    }

    [Test]
    public void Timer_RestartAndElapsedMilliseconds_Work()
    {
        var timer = new QaaS.Framework.Policies.Extentions.Stopwatch.Timer();
        timer.Restart();
        Thread.Sleep(2);

        Assert.That(timer.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void PolicyBuilder_Build_ReturnsCountPolicy()
    {
        var builder = new PolicyBuilder();
        builder.Configure(new CountPolicyConfig { Count = 3 });

        var policy = builder.Build();

        Assert.That(policy, Is.TypeOf<CountPolicy>());
    }

    [Test]
    public void PolicyBuilder_Build_ReturnsTimeoutPolicy()
    {
        var builder = new PolicyBuilder();
        builder.Configure(new TimeoutPolicyConfig { TimeoutMs = 1 });

        var policy = builder.Build();

        Assert.That(policy, Is.TypeOf<TimeoutPolicy>());
    }

    [Test]
    public void PolicyBuilder_Build_ReturnsLoadBalancePolicies()
    {
        var builder = new PolicyBuilder();

        builder.Configure(new LoadBalancePolicyConfig { Rate = 5, TimeIntervalMs = 1000 });
        Assert.That(builder.Build(), Is.TypeOf<LoadBalancePolicy>());

        builder.Configure(new IncreasingLoadBalancePolicyConfig
        {
            StartRate = 1,
            MaxRate = 2,
            RateIncrease = 1,
            RateIncreaseIntervalMs = 100,
            TimeIntervalMs = 1000
        });
        Assert.That(builder.Build(), Is.TypeOf<IncreasingLoadBalancePolicy>());

        builder.Configure(new AdvancedLoadBalancePolicyConfig
        {
            Stages =
            [
                new StageConfig { Rate = 1, Amount = 1, TimeIntervalMs = 1000 },
                new StageConfig { Rate = 2, Amount = 2, TimeIntervalMs = 1000 }
            ]
        });
        Assert.That(builder.Build(), Is.TypeOf<AdvancedLoadBalancePolicy>());
    }

    [Test]
    public void PolicyBuilder_Build_ThrowsWhenMultiplePoliciesConfigured()
    {
        var builder = new PolicyBuilder();
        var type = typeof(PolicyBuilder);
        type.GetProperty("Count", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(builder, new CountPolicyConfig { Count = 1 });
        type.GetProperty("Timeout", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(builder, new TimeoutPolicyConfig { TimeoutMs = 1 });

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Test]
    public void BuildPolicies_ReturnsNullForNullBuilders_AndPolicyForInput()
    {
        Assert.That(PolicyBuilder.BuildPolicies(null), Is.Null);

        var policy = PolicyBuilder.BuildPolicies(
        [
            new PolicyBuilder().Configure(new CountPolicyConfig { Count = 10 })
        ]);

        Assert.That(policy, Is.Not.Null);
    }

    [Test]
    public void Exceptions_ContainExpectedMessages()
    {
        var count = new CountStopException(5, CommunicationType.read);
        var timeout = new TimeoutStopException(TimeSpan.FromMilliseconds(10));

        Assert.That(count.Message, Does.Contain("5 messages"));
        Assert.That(timeout.Message, Does.Contain("timeout"));
    }

    [Test]
    public void PoliciesEnum_HasExpectedValues()
    {
        var values = Enum.GetNames(typeof(Policies));

        Assert.That(values, Does.Contain(nameof(Policies.Count)));
        Assert.That(values, Does.Contain(nameof(Policies.Timeout)));
        Assert.That(values, Does.Contain(nameof(Policies.LoadBalance)));
    }
}
