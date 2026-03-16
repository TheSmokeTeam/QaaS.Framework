using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Policies.ConfigurationObjects;

namespace QaaS.Framework.Policies.Tests;

[TestFixture]
public class PolicyValidationCoverageTests
{
    [Test]
    public void AdvancedLoadBalancePolicyConfig_RequiresAmountOrTimeoutPerStage()
    {
        var invalidConfig = new AdvancedLoadBalancePolicyConfig
        {
            Stages = [new StageConfig { Rate = 1, TimeIntervalMs = 1000 }]
        };
        var validConfig = new AdvancedLoadBalancePolicyConfig
        {
            Stages =
            [
                new StageConfig { Rate = 1, Amount = 1, TimeIntervalMs = 1000 },
                new StageConfig { Rate = 2, TimeoutMs = 10, TimeIntervalMs = 1000 }
            ]
        };

        var invalidResults = new List<ValidationResult>();
        var validResults = new List<ValidationResult>();

        var invalid = Validator.TryValidateObject(invalidConfig, new ValidationContext(invalidConfig), invalidResults, true);
        var valid = Validator.TryValidateObject(validConfig, new ValidationContext(validConfig), validResults, true);

        Assert.Multiple(() =>
        {
            Assert.That(invalid, Is.False);
            Assert.That(invalidResults.Single().ErrorMessage, Does.Contain("Either 'TimeoutMs' or 'Amount'"));
            Assert.That(valid, Is.True);
            Assert.That(validResults, Is.Empty);
        });
    }

    [Test]
    public void IncreasingLoadBalancePolicyConfig_RejectsStartRateGreaterThanMaxRate()
    {
        var invalidConfig = new IncreasingLoadBalancePolicyConfig
        {
            StartRate = 5,
            MaxRate = 4,
            RateIncrease = 1
        };
        var validConfig = new IncreasingLoadBalancePolicyConfig
        {
            StartRate = 4,
            MaxRate = 5,
            RateIncrease = 1
        };

        var invalidResults = new List<ValidationResult>();
        var validResults = new List<ValidationResult>();

        var invalid = Validator.TryValidateObject(invalidConfig, new ValidationContext(invalidConfig), invalidResults, true);
        var valid = Validator.TryValidateObject(validConfig, new ValidationContext(validConfig), validResults, true);

        Assert.Multiple(() =>
        {
            Assert.That(invalid, Is.False);
            Assert.That(invalidResults.Single().ErrorMessage, Does.Contain("'StartRate' cannot be greater than 'MaxRate'"));
            Assert.That(valid, Is.True);
            Assert.That(validResults, Is.Empty);
        });
    }

    [Test]
    public void TimeoutPolicy_RunChain_RemainsTrueBeforeTimeoutExpires()
    {
        var policy = new TimeoutPolicy(1000);
        policy.SetupChain();

        var shouldContinue = policy.RunChain();

        Assert.That(shouldContinue, Is.True);
    }
}
