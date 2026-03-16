using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Framework.Policies.ConfigurationObjects;

[PropertyComparison(nameof(StartRate), nameof(MaxRate), PropertyComparisonOperator.LessThanOrEqual,
    ErrorMessage = $"'{nameof(StartRate)}' cannot be greater than '{nameof(MaxRate)}'.")]
public class IncreasingLoadBalancePolicyConfig : IPolicyConfig
{
    [Required, Range(1, ulong.MaxValue),
     Description($"The initial amount of actions to perform every {nameof(TimeIntervalMs)} milliseconds")]
    public ulong? StartRate { get; set; }

    [Required, Range(1, ulong.MaxValue),
     Description($"The maximum amount of actions to perform every {nameof(TimeIntervalMs)} milliseconds")]
    public ulong? MaxRate { get; set; }

    [Range(1, ulong.MaxValue), Description($"How much to increase the rate every {nameof(RateIncreaseIntervalMs)}"),
     DefaultValue(1)]
    public ulong? RateIncrease { get; set; } = 1;

    [Range(1, double.MaxValue), Description($"How often to increase the rate by {nameof(RateIncrease)} in milliseconds"),
     DefaultValue(1000)]
    public double RateIncreaseIntervalMs { get; set; } = 1000;

    [Range(1, ulong.MaxValue), Description("The time in milliseconds to perform `Rate` actions in"),
     DefaultValue(1000)]
    public ulong TimeIntervalMs { get; set; } = 1000;
}
