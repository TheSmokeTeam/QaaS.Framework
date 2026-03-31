using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations;
using QaaS.Framework.Configurations.CustomAttributes;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;

namespace QaaS.Framework.SDK.Hooks.Probe;

[JsonSchema]
public abstract class BaseProbe<TConfiguration> : IProbe where TConfiguration : new()
{
    public Context Context { get; set; } = null!;
    
    /// <summary>
    /// The relevant configuration for this probe's scope loaded and validated
    /// into a configuration object
    /// </summary>
    public TConfiguration Configuration { get; set; } = default!;
    
    /// <summary>
    /// The options of the binder that binds the IConfiguration to the <see cref="Configuration"/>
    /// </summary>
    protected virtual BinderOptions GetConfigurationBinderOptions() => new()
    {
        ErrorOnUnknownConfiguration = true
    };
    
    /// <inheritdoc />
    public virtual List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration)
    {
        // Load configuration to c# object
        Configuration = configuration.BindToObject<TConfiguration>(GetConfigurationBinderOptions(), Context.Logger);

        // Validate loaded configuration
        var validationResults = new List<ValidationResult>();
        ValidationUtils.TryValidateObjectRecursive(Configuration, validationResults);

        return validationResults;
    }

    /// <inheritdoc /> 
    public abstract void Run(IImmutableList<SessionData> sessionDataList, IImmutableList<DataSource> dataSourceList);
}
