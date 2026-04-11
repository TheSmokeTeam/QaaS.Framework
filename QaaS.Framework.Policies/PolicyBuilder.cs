using System.ComponentModel;
using QaaS.Framework.Configurations;
using QaaS.Framework.Policies.AdvancedLoadBalance;
using QaaS.Framework.Policies.ConfigurationObjects;

namespace QaaS.Framework.Policies;

public class PolicyBuilder
{
    public CountPolicyConfig? Count { get; internal set; }
    public TimeoutPolicyConfig? Timeout { get; internal set; }
    [Description("This policy is in charge of controlling the rate in which the action is repeatedly executed")]
    public LoadBalancePolicyConfig? LoadBalance { get; internal set; }
    [Description(
         "This policy is in charge of controlling the rate in which the action is repeatedly executed and increasing it overtime")]
    public IncreasingLoadBalancePolicyConfig? IncreasingLoadBalance { get; internal set; }
    [Description(
         "This policy executes actions in separate stages, each stage has a rate in which to execute" +
         " the actions included in it and a count or timeout to know after how many actions or after" +
         " how much time to end the stage and move to the next.")]
    public AdvancedLoadBalancePolicyConfig? AdvancedLoadBalance { get; internal set; }

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
        ArgumentNullException.ThrowIfNull(config);

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
    /// Sets the count policy configuration on the current Framework policy builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework policy builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Policies" />
    public PolicyBuilder WithCount(CountPolicyConfig config) => Configure(config);

    /// <summary>
    /// Sets the timeout policy configuration on the current Framework policy builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework policy builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Policies" />
    public PolicyBuilder WithTimeout(TimeoutPolicyConfig config) => Configure(config);

    /// <summary>
    /// Sets the load-balance policy configuration on the current Framework policy builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework policy builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Policies" />
    public PolicyBuilder WithLoadBalance(LoadBalancePolicyConfig config) => Configure(config);

    /// <summary>
    /// Sets the increasing load-balance policy configuration on the current Framework policy builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework policy builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Policies" />
    public PolicyBuilder WithIncreasingLoadBalance(IncreasingLoadBalancePolicyConfig config) => Configure(config);

    /// <summary>
    /// Sets the advanced load-balance policy configuration on the current Framework policy builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework policy builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Policies" />
    public PolicyBuilder WithAdvancedLoadBalance(AdvancedLoadBalancePolicyConfig config) => Configure(config);

    /// <summary>
    /// Updates the configuration currently stored on the Framework policy builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework policy builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Policies" />
    public PolicyBuilder UpdateConfiguration(object configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var currentConfig = GetConfiguredPolicy();
        if (configuration is IPolicyConfig typedConfiguration)
        {
            return Configure(currentConfig == null
                ? typedConfiguration
                : currentConfig.UpdateConfiguration(typedConfiguration));
        }

        if (currentConfig == null)
            throw new InvalidOperationException(
                "Policy configuration is not set and cannot be inferred from an object patch. Configure a concrete policy configuration first.");
        return Configure(currentConfig.UpdateConfiguration(configuration));
    }

    private IPolicyConfig? GetConfiguredPolicy()
    {
        if (Count != null) return Count;
        if (Timeout != null) return Timeout;
        if (LoadBalance != null) return LoadBalance;
        if (IncreasingLoadBalance != null) return IncreasingLoadBalance;
        return AdvancedLoadBalance;
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
