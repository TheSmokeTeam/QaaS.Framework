using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.SDK.ContextObjects;

namespace QaaS.Framework.SDK.Hooks;

/// <summary>
/// Represents any code hook that may be plugged and used as part of a QaaS project run
/// </summary>
public interface IHook
{
    /// <summary>
    /// The context relevant to the hook
    /// </summary>
    public Context Context { get; set; }
    
    /// <summary>
    /// Loads and validates the given configuration
    /// </summary>
    /// <param name="configuration"> The relevant configuration for this assertion scope </param>
    /// <returns> A list that contains the configuration's validation results </returns>
    public List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration);
}