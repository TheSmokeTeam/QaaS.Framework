using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Policies.ConfigurationObjects;

public record LoadBalancePolicyConfig : IPolicyConfig
{
    [Required, Range(1, double.MaxValue),
     Description($"The amount of actions to perform every `{nameof(TimeIntervalMs)}` milliseconds")]
    public double? Rate { get; set; }
    
    [Range(1, ulong.MaxValue), Description($"The time in milliseconds to perform `{nameof(Rate)}` actions in"),
     DefaultValue(1000)]
    public ulong TimeIntervalMs { get; set; } = 1000;
}