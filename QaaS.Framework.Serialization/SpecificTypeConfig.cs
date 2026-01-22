using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace QaaS.Framework.Serialization;

/// <summary>
/// Configuration representing a specific C# Type
/// </summary>
public record SpecificTypeConfig
{
    [Description("The name of the assembly the type is located in, If no value is given" +
                 " by default tries to take the entry assembly")]
    public string? AssemblyName { get; set; }
    
    [Required, Description("The full name (including path) of the type")]
    public string? TypeFullName { get; set; }

    /// <summary>
    /// Gets the type configured in the record
    /// </summary>
    public Type GetConfiguredType()
    {
        AssemblyName ??= Assembly.GetEntryAssembly()?.GetName().FullName ?? throw new ArgumentException(
            "Could not find Entry Assembly, was called from an unmanaged code");
        
        var assembly = Assembly.Load(AssemblyName!);
        var type = assembly.GetType(TypeFullName!, throwOnError: true);
        if (type == null)
            throw new ArgumentException($"Type {TypeFullName} in assembly {AssemblyName} could not be found");
        return type;
    }
}