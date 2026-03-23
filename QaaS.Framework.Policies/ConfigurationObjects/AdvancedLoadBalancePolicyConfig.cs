using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Policies.AdvancedLoadBalance;

namespace QaaS.Framework.Policies.ConfigurationObjects;

public record AdvancedLoadBalancePolicyConfig : IPolicyConfig, IValidatableObject
{
    [Description(
         "The stages of publishing information, in each stage the messages will be published" +
         "with a given rate untill 'Amount' messages are generated or untill 'TimeoutMs' is reached")]
    public StageConfig[]? Stages { get; internal set; }
    public IReadOnlyList<StageConfig> ReadStages() => Stages ?? [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Stages == null)
        {
            yield return new ValidationResult("The Stages field is required.");
            yield break;
        }

        if (Stages.Any(instance => !instance.Amount.HasValue && !instance.TimeoutMs.HasValue))
            yield return new ValidationResult("Either 'TimeoutMs' or 'Amount' must have a value.");
    }
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

