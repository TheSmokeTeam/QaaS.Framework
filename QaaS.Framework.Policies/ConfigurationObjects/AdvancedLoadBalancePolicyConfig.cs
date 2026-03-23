using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Policies.AdvancedLoadBalance;

namespace QaaS.Framework.Policies.ConfigurationObjects;

public record AdvancedLoadBalancePolicyConfig : IPolicyConfig
{
    [EitherTimeoutMsOrAmountRequired]
    [Required, Description(
         "The stages of publishing information, in each stage the messages will be published" +
         "with a given rate untill 'Amount' messages are generated or untill 'TimeoutMs' is reached")]
    internal StageConfig[]? Stages { get; set; }

    public IReadOnlyList<StageConfig> ReadStages() => Stages ?? [];
}

/// <summary>
/// Information about each stage
/// </summary>
public record StageConfig
{
    [Required, Range(1, ulong.MaxValue),
     Description($"The amount of actions to perform every `{nameof(TimeIntervalMs)}` milliseconds")]
    public double? Rate { get; set; }

    [Range(1, uint.MaxValue), Description("The number of times to perform action")]
    public uint? Amount { get; set; }

    [Range(uint.MinValue, uint.MaxValue),
     Description("The time in milliseconds before stopping the communication action")]
    public uint? TimeoutMs { get; set; }

    [Range(1, double.MaxValue), Description($"The time in milliseconds to perform `{nameof(Rate)}` actions in"),
     DefaultValue(1000)]
    public ulong TimeIntervalMs { get; set; } = 1000;
}

internal class EitherTimeoutMsOrAmountRequiredAttribute : ValidationAttribute
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
        var config = (AdvancedLoadBalancePolicyConfig)validationContext.ObjectInstance;
        return config.Stages!.Any(instance => !instance.Amount.HasValue && !instance.TimeoutMs.HasValue)
            ? new ValidationResult("Either 'TimeoutMs' or 'Amount' must have a value.")
            : ValidationResult.Success!;
    }
}
