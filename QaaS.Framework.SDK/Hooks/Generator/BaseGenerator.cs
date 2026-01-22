using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations;
using QaaS.Framework.Configurations.CustomAttributes;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;

namespace QaaS.Framework.SDK.Hooks.Generator;

/// <summary>
/// Base generator to inherit from that gives the functionality of generating data with a given configuration object
/// </summary>
/// <typeparam name="TConfiguration"> The type of the generator's configuration object </typeparam>
[JsonSchema]
public abstract class BaseGenerator<TConfiguration>: IGenerator where TConfiguration : new()
{
    /// <inheritdoc />
    public Context Context { get; set; } = null!;

    /// <summary>
    /// The relevant configuration for this generator's scope loaded and validated
    /// into a configuration object
    /// </summary>
    public TConfiguration Configuration { get; set; }

    /// <summary>
    /// The options of the binder that binds the IConfiguration to the <see cref="Configuration"/>
    /// </summary>
    protected virtual BinderOptions GetConfigurationBinderOptions() => new()
    {
        ErrorOnUnknownConfiguration = true
    };
    
    /// <inheritdoc />
    public List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration)
    {
        // Load configuration to c# object
        Configuration = configuration.BindToObject<TConfiguration>(GetConfigurationBinderOptions(), Context.Logger);

        // Validate loaded configuration
        var validationResults = new List<ValidationResult>();
        ValidationUtils.TryValidateObjectRecursive(Configuration, validationResults);

        return validationResults;
    }
    
    /// <inheritdoc /> 
    public abstract IEnumerable<Data<object>> Generate(IImmutableList<SessionData> sessionDataList,
        IImmutableList<DataSource> dataSourceList);
}