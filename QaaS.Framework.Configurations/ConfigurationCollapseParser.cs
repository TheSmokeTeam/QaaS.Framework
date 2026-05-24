using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations.CustomExceptions;

namespace QaaS.Framework.Configurations;

/// <summary>
/// Class that contains functionality for parsing the collapse of a configuration.
/// </summary>
public static class ConfigurationCollapseParser
{
    /// <summary>
    /// Collapses shift left arrows ('<<') in a configuration.
    /// </summary>
    /// <param name="configuration"> The raw configuration object before collapsing arrows </param>
    /// <returns> Configuration with collapsed arrows </returns>
    /// <remarks>
    /// YamlDotNet does not natively resolve YAML "<<:" merge keys; they leak through into
    /// IConfiguration as literal "<<" path segments and, when multiple merge sources are listed,
    /// as positional indices (e.g. "Foo:&lt;&lt;:0:Bar"). The collapse rules:
    /// <list type="bullet">
    ///   <item>At each tree level, children whose key is exactly "&lt;&lt;" contribute their own
    ///   children to the parent level with an incremented arrow count.</item>
    ///   <item>Among siblings sharing the same key, the entry with the lowest arrow count wins
    ///   (local values beat merged values, and shallow merges beat deeper merges).</item>
    ///   <item>A leaf "&lt;&lt;" key with a scalar value is illegal — it must be a mapping or list.</item>
    /// </list>
    /// The fast path short-circuits when no key contains "&lt;&lt;" because the entire collapse is a
    /// no-op for the typical case of a YAML that doesn't use merge keys.
    /// </remarks>
    public static IConfiguration CollapseShiftLeftArrowsInConfiguration(this IConfiguration configuration)
    {
        if (!configuration.AsEnumerable().Any(entry =>
                entry.Key.Contains(ConfigurationConstants.CollapseString, StringComparison.Ordinal)))
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
