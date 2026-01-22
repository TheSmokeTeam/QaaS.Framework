using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Policies.ConfigurationObjects;

public class IncreasingLoadBalancePolicyConfig : IPolicyConfig
{
    [MaxRateBiggerThenMinRateValidation, Required, Range(1, ulong.MaxValue),
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

/// <summary>
/// Validation of all of the Stages
/// </summary>
internal class MaxRateBiggerThenMinRateValidation : ValidationAttribute
{
    /// <summary>
    /// Check that one of the two parameters 'TimeoutMS' or 'Count' exists for the policy
    /// configuration to be valid
    /// </summary>
    /// <param name="value">instance if the configuration</param>
    /// <param name="validationContext">context of the configuration</param>
    /// <returns>true if the configuration is valid, false otherwise</returns>
    protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
    {
        var config = (IncreasingLoadBalancePolicyConfig)validationContext.ObjectInstance;
        return config.StartRate > config.MaxRate
            ? new ValidationResult("'MinRate' cannot be bigger then 'MaxRate'")
            : ValidationResult.Success!;
    }
}