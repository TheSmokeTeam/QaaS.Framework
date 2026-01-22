using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations.CustomExceptions;

namespace QaaS.Framework.Configurations;

/// <summary>
/// Class that contains functionality for parsing the collapse of a configuration 
/// </summary>
public static class ConfigurationCollapseParser
{
    /// <summary>
    /// Collapses shift left arrows ('<<') in a configuration
    /// </summary>
    /// <param name="configuration"> The raw configuration object before collapsing arrows </param>
    /// <returns> Configuration with collapsed arrows </returns>
    public static IConfiguration CollapseShiftLeftArrowsInConfiguration(this IConfiguration configuration)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(GetConfigurationPathsAndValuesWithCollapsedArrows(configuration)).Build();
    }
        
    private static IEnumerable<KeyValuePair<string, string>> GetConfigurationPathsAndValuesWithCollapsedArrows
        (IConfiguration configurationRoot)
    {
        var configurationPathsAndValues = new List<KeyValuePair<string, string>>();

        return GetDirectChildrenAfterCollapsingArrows(configurationRoot)
            .Aggregate(configurationPathsAndValues,
                (current, configurationSection) =>
                    current.Concat(GetConfigurationPathsAndValuesWithCollapsedArrows(configurationSection)).ToList());
    }

    private static IEnumerable<KeyValuePair<string, string>> GetConfigurationPathsAndValuesWithCollapsedArrows(
        IConfigurationSection configurationSection, string configurationPath = "")
    {
        var configurationValues = new List<KeyValuePair<string, string>>();
        var reachedConfigurationEndpoint = !configurationSection.GetChildren().Any();
        if (reachedConfigurationEndpoint)
        {
            configurationValues.Add(new KeyValuePair<string, string>(configurationPath + 
                                                                     configurationSection.Key,
                configurationSection.Value));
            return configurationValues;
        }
            
        var childrenConfigurationPath = $"{configurationPath}{configurationSection.Key}:";

        return GetDirectChildrenAfterCollapsingArrows(configurationSection)
            .Aggregate(configurationValues, (current, child) => 
                current.Concat(GetConfigurationPathsAndValuesWithCollapsedArrows(child, childrenConfigurationPath))
                .ToList());
    }

    private static IEnumerable<IConfigurationSection> GetDirectChildrenAfterCollapsingArrows(
        IConfiguration configuration)
    {
        // If there are multiple children with the same key - select the one with the least collapsed 'CollapseString' keys
        return GetDirectChildrenAndNumberOfCollapsedArrows(configuration)
            .GroupBy(child => child.Key.Key)
            .Select(group => group.MinBy(child =>
                child.Value).Key)
            .ToList();
    }
        
    private static IEnumerable<KeyValuePair<IConfigurationSection, int>> GetDirectChildrenAndNumberOfCollapsedArrows(
        IConfiguration configuration, int numberOfArrows = 0)
    {
        var directChildren = configuration.GetChildren().ToList();
        var directChildrenAfterCollapsingArrows = new List<KeyValuePair<IConfigurationSection, int>>();
        foreach (var child in directChildren)
        {
            if (!child.Key.Equals(ConfigurationConstants.CollapseString))
            {
                directChildrenAfterCollapsingArrows.Add(new KeyValuePair<IConfigurationSection, int>(child, 
                    numberOfArrows));
                continue;
            }

            // If the key of the child is 'CollapseString' and the child is a configuration endpoint
            if (!child.GetChildren().Any())
            {
                throw new InvalidConfigurationsException($"The collapse key '{ConfigurationConstants.CollapseString}' at " +
                                                         $"`{child.Path}` has the value `{child.Value}`." +
                                                         $" The collapse key '{ConfigurationConstants.CollapseString}' must" +
                                                         $" contain a dictionary or a list, not a value");
            }
            // Recursively move to the children of the child until the key is not 'CollapseString'
            directChildrenAfterCollapsingArrows = directChildrenAfterCollapsingArrows
                .Concat(GetDirectChildrenAndNumberOfCollapsedArrows(
                    child, numberOfArrows + 1)).ToList();
        }
        return directChildrenAfterCollapsingArrows;
    }
}