using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Framework.SDK.Hooks.Processor;

/// <summary>
/// Base transaction stub to inherit from that gives the functionality of exercising transaction data.
/// </summary>
/// <typeparam name="TConfiguration"> The type of the stub's configuration object </typeparam>
public abstract class BaseTransactionProcessor<TConfiguration> : ITransactionProcessor where TConfiguration : new()
{
    /// <inheritdoc />
    public Context Context { get; set; } = null!;

    /// <summary>
    /// The relevant configuration for this stub's scope loaded and validated into a configuration object.
    /// </summary>
    public TConfiguration Configuration { get; internal set; } = default!;
 
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
    public abstract Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData);
}
