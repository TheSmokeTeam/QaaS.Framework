using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations;
using QaaS.Framework.Configurations.CustomAttributes;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;

namespace QaaS.Framework.SDK.Hooks.Assertion;

/// <summary>
/// Base assertion to inherit from that gives the functionality of performing an assertion
/// with a given configuration object
/// </summary>
/// <typeparam name="TConfiguration"> The type of the assertion's configuration object </typeparam>
[JsonSchema]
public abstract class BaseAssertion<TConfiguration>: IAssertion where TConfiguration : new()
{
    /// <inheritdoc />
    public Context Context { get; set; } = null!;
    
    /// <inheritdoc />
    public string? AssertionMessage { get; set; }
    
    /// <inheritdoc />
    public string? AssertionTrace { get; set; }

    /// <inheritdoc />
    public IList<AssertionAttachment> AssertionAttachments { get; set; } = [];

    /// <inheritdoc />
    public AssertionStatus? AssertionStatus { get; set; }

    /// <inheritdoc />
    public abstract bool Assert(IImmutableList<SessionData> sessionDataList,
        IImmutableList<DataSource> dataSourceList);

    /// <summary>
    /// The relevant configuration for this assertion's scope loaded and validated
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
}
