using System.ComponentModel;
using QaaS.Framework.Policies.AdvancedLoadBalance;
using QaaS.Framework.Policies.ConfigurationObjects;

namespace QaaS.Framework.Policies;

public class PolicyBuilder
{
    internal CountPolicyConfig? Count { get; set; }

    internal TimeoutPolicyConfig? Timeout { get; set; }

    [Description("This policy is in charge of controlling the rate in which the action is repeatedly executed")]
    internal LoadBalancePolicyConfig? LoadBalance { get; set; }

    [Description(
         "This policy is in charge of controlling the rate in which the action is repeatedly executed and increasing it overtime")]
    internal IncreasingLoadBalancePolicyConfig? IncreasingLoadBalance { get; set; }

    [Description(
         "This policy executes actions in separate stages, each stage has a rate in which to execute" +
         " the actions included in it and a count or timeout to know after how many actions or after" +
         " how much time to end the stage and move to the next.")]
    internal AdvancedLoadBalancePolicyConfig? AdvancedLoadBalance { get; set; }

    private PolicyBuilder Reset()
    {
        Count = null;
        Timeout = null;
        LoadBalance = null;
        IncreasingLoadBalance = null;
        AdvancedLoadBalance = null;
        return this;
    }
    
    /// <summary>
    /// Sets the configuration currently stored on the Framework policy builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework policy builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Policies" />
    public PolicyBuilder Configure(IPolicyConfig config)
    {
        Reset();
        switch (config)
        {
            case CountPolicyConfig countPolicyConfig:
                Count = countPolicyConfig;
                break;
            case TimeoutPolicyConfig timeoutPolicyConfig:
                Timeout = timeoutPolicyConfig;
                break;
            case LoadBalancePolicyConfig loadBalancePolicyConfig:
                LoadBalance = loadBalancePolicyConfig;
                break;
            case IncreasingLoadBalancePolicyConfig increasingLoadBalancePolicyConfig:
                IncreasingLoadBalance = increasingLoadBalancePolicyConfig;
                break;
            case AdvancedLoadBalancePolicyConfig advancedLoadBalancePolicyConfig:
                AdvancedLoadBalance = advancedLoadBalancePolicyConfig;
                break;
            default:
                throw new InvalidOperationException($"Policy configuration type {config.GetType()} not supported");
        }

        return this;
    }

    /// <summary>
    /// Builds the configured Framework policy builder output from the current state.
    /// </summary>
    /// <remarks>
    /// Call this after the fluent configuration is complete. The method validates the accumulated state and materializes the runtime or immutable configuration object represented by the builder.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Policies" />
    public Policy Build()
    {
        IPolicyConfig? type = null;
        var allTypes = new List<IPolicyConfig?>()
            { Count, LoadBalance, IncreasingLoadBalance, AdvancedLoadBalance, Timeout };
        type = allTypes.FirstOrDefault(configuredType => configuredType != null) ??
               throw new InvalidOperationException($"Missing supported type for policy");
        if (allTypes.Count(config => config != null) > 1)
        {
            var conflictingConfigs = allTypes
                .Where(config => config != null)
                .Select(config => config!.GetType().Name)
                .ToArray();
            throw new InvalidOperationException(
                $"Multiple configurations provided for Policy: {string.Join(", ", conflictingConfigs)}. " +
                "Only one type is allowed at a time.");
        }
        return type switch
        {
            CountPolicyConfig => new CountPolicy(Count!.Count),
            TimeoutPolicyConfig => new TimeoutPolicy(Timeout!.TimeoutMs),
            LoadBalancePolicyConfig => new LoadBalancePolicy(LoadBalance!.Rate!.Value, LoadBalance!.TimeIntervalMs),
            AdvancedLoadBalancePolicyConfig => new AdvancedLoadBalancePolicy(AdvancedLoadBalance!.Stages!),
            IncreasingLoadBalancePolicyConfig => new IncreasingLoadBalancePolicy(IncreasingLoadBalance!.StartRate!.Value,
                IncreasingLoadBalance.TimeIntervalMs, IncreasingLoadBalance.MaxRate!.Value,
                IncreasingLoadBalance.RateIncrease!.Value, IncreasingLoadBalance.RateIncreaseIntervalMs),
            _ => throw new ArgumentException("Exception: Policy must have a type.")
        };
    }

    /// <summary>
    /// Builds a policy chain from the supplied policy builder collection.
    /// </summary>
    /// <remarks>
    /// This helper lets callers collapse several fluent policy builders into the policy chain consumed by the runtime configuration surface.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Policies" />
    public static Policy? BuildPolicies(PolicyBuilder[]? policyBuilders)
    {
        Policy? policies = null; // create policies from builders
        if (policyBuilders == null) return policies;
        foreach (var policyBuilder in policyBuilders)
            policies = policies == null ? policyBuilder.Build() : policies.Add(policyBuilder.Build());
        

        return policies;
    }
}
