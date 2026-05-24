using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations.CustomExceptions;

namespace QaaS.Framework.Configurations;

/// <summary>
/// Class that contains functionality for parsing the collapse of a configuration.
/// </summary>
public static class ConfigurationCollapseParser
{
    /// <summary>
    /// Collapses YAML merge-key segments ('<<') leaked through by YamlDotNet into the configuration tree,
    /// preferring the most specific value when several paths collapse to the same key. Returns the
    /// configuration unchanged when no merge keys are present.
    /// </summary>
    /// <param name="configuration"> The raw configuration object before collapsing arrows </param>
    /// <returns> Configuration with collapsed arrows </returns>
    public static IConfiguration CollapseShiftLeftArrowsInConfiguration(this IConfiguration configuration)
    {
        var configurationHasNoMergeKeys = !configuration.AsEnumerable().Any(configurationEntry =>
            configurationEntry.Key.Contains(ConfigurationConstants.CollapseString, StringComparison.Ordinal));
        if (configurationHasNoMergeKeys)
            return configuration;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(GetConfigurationPathsAndValuesWithCollapsedArrows(configuration))
            .Build();
    }

    private static IEnumerable<KeyValuePair<string, string?>> GetConfigurationPathsAndValuesWithCollapsedArrows(
        IConfiguration configurationRoot)
    {
        var configurationPathsAndValues = new List<KeyValuePair<string, string?>>();

        return GetDirectChildrenAfterCollapsingArrows(configurationRoot)
            .Aggregate(
                configurationPathsAndValues,
                (current, configurationSection) =>
                    current.Concat(GetConfigurationPathsAndValuesWithCollapsedArrows(configurationSection)).ToList());
    }

    private static IEnumerable<KeyValuePair<string, string?>> GetConfigurationPathsAndValuesWithCollapsedArrows(
        IConfigurationSection configurationSection, string configurationPath = "")
    {
        var configurationValues = new List<KeyValuePair<string, string?>>();
        var reachedConfigurationEndpoint = !configurationSection.GetChildren().Any();
        if (reachedConfigurationEndpoint)
        {
            configurationValues.Add(new KeyValuePair<string, string?>(
                configurationPath + configurationSection.Key,
                configurationSection.Value));
            return configurationValues;
        }

        var childrenConfigurationPath = $"{configurationPath}{configurationSection.Key}:";

        return GetDirectChildrenAfterCollapsingArrows(configurationSection)
            .Aggregate(
                configurationValues,
                (current, child) =>
                    current.Concat(GetConfigurationPathsAndValuesWithCollapsedArrows(child, childrenConfigurationPath))
                        .ToList());
    }

    private static IEnumerable<IConfigurationSection> GetDirectChildrenAfterCollapsingArrows(
        IConfiguration configuration) =>
        // Group same-keyed siblings and prefer the most specific (lowest arrow-count) one — that's
        // the local value over a merged value, or a shallow merge over a deeper one.
        GetDirectChildrenAndNumberOfCollapsedArrows(configuration)
            .GroupBy(child => child.Key.Key)
            .Select(group => group.MinBy(child => child.Value).Key)
            .ToList();

    private static IEnumerable<KeyValuePair<IConfigurationSection, int>> GetDirectChildrenAndNumberOfCollapsedArrows(
        IConfiguration configuration, int numberOfArrows = 0)
    {
        var directChildren = configuration.GetChildren().ToList();
        var directChildrenAfterCollapsingArrows = new List<KeyValuePair<IConfigurationSection, int>>();
        foreach (var child in directChildren)
        {
            if (!child.Key.Equals(ConfigurationConstants.CollapseString))
            {
                directChildrenAfterCollapsingArrows.Add(
                    new KeyValuePair<IConfigurationSection, int>(child, numberOfArrows));
                continue;
            }

            if (!child.GetChildren().Any())
                throw new InvalidConfigurationsException(
                    $"The collapse key '{ConfigurationConstants.CollapseString}' at `{child.Path}` " +
                    $"has the value `{child.Value}`. The collapse key '{ConfigurationConstants.CollapseString}' " +
                    $"must contain a dictionary or a list, not a value");

            // Recursively bubble the children of `<<` up to the parent level with an incremented
            // arrow count, so they can compete against this level's local children in the grouping above.
            directChildrenAfterCollapsingArrows = directChildrenAfterCollapsingArrows
                .Concat(GetDirectChildrenAndNumberOfCollapsedArrows(child, numberOfArrows + 1))
                .ToList();
        }
        return directChildrenAfterCollapsingArrows;
    }
}
