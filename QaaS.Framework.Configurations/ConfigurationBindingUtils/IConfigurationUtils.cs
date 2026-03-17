using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations;

namespace QaaS.Framework.Configurations.ConfigurationBindingUtils;

/// <summary>
/// Utility methods for IConfiguration
/// </summary>
public static class IConfigurationUtils
{
    /// <summary>
    /// Merges a partial configuration object into the existing <see cref="IConfiguration"/>.
    /// Existing values are preserved when the incoming object leaves a field at its type default.
    /// </summary>
    /// <returns>The updated configuration</returns>
    public static IConfiguration BindConfigurationObjectToIConfiguration(this IConfiguration configuration,
        object? configurationObject) => configuration.MergeConfigurationObjectIntoIConfiguration(configurationObject);

    /// <summary>
    /// Returns Dictionary representation of given IConfiguration
    /// </summary>
    /// <param name="configuration">The configuration to return the Dictionary representation for</param>
    /// <returns>Dictionary representation of the configuration</returns>
    public static Dictionary<string, object?> GetDictionaryFromConfiguration(this IConfiguration? configuration)
    {
        var configurationDictionary = DictionaryUtils.CreateConfigurationDictionary<object?>();
        if (configuration == null)
            return configurationDictionary;
        foreach (var section in configuration.GetChildren())
            configurationDictionary[section.Key] = ConvertConfigurationSectionToObject(section);
        return configurationDictionary;
    }

    /// <summary>
    /// Return an object representation of the given IConfigurationSection
    /// </summary>
    /// <param name="section">The configuration section</param>
    /// <returns>Dictionary representation of the configuration section</returns>
    private static object? ConvertConfigurationSectionToObject(IConfigurationSection section)
    {
        var children = section.GetChildren().ToList();

        // if the section has no children, then we assume that this is a value
        // and we return the value
        if (!children.Any())
            return section.Value;
        
        // if the keys are all integers in counting order, then we assume that this is a list
        if(children.Any() && children.All(c => int.TryParse(c.Key, out _)) && 
           children.Select(c => int.Parse(c.Key)).SequenceEqual(Enumerable.Range(0, children.Count)))
            return children.Select(ConvertConfigurationSectionToObject).ToList();

        var configurationDictionary = DictionaryUtils.CreateConfigurationDictionary<object?>();
        // if the section has children, then we assume that this is a dictionary
        // and we recursively convert the children to a dictionary
        foreach (var child in children)
            configurationDictionary[child.Key] = ConvertConfigurationSectionToObject(child);

        return configurationDictionary;
    }
}
