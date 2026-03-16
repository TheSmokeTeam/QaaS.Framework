using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Framework.Configurations.References;

/// <summary>
/// A reference configuration fields used to resolve a reference.
/// A reference is the name of a configuration that will be `pushed` at the end of another configuration's certain lists,
/// it will push the items from the configuration to the end OR instead of a certain placeholder of the other configuration's
/// lists.
/// </summary>
public record ReferenceConfig
{
    /// <summary>
    /// The keyword that indicates where to add items to the configured lists paths, if the keyword is not present in a certain list
    /// will by default stack it at the end of it
    /// </summary>
    [Required, MinLength(1)]
    public string ReferenceReplaceKeyword { get; init; } = null!;
    
    /// <summary>
    /// The paths to the YAML configuration files that create the referenced configuration
    /// </summary>
    [Required, MinLength(1), AllPathsInEnumerableValid, UniqueItemsInEnumerable]
    public IList<string>? ReferenceFilesPaths { get; init; }
}
