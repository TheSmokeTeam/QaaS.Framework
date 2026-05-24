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
        var entries = configuration.AsEnumerable().ToList();
        if (!entries.Any(e => e.Key.Contains(ConfigurationConstants.CollapseString, StringComparison.Ordinal)))
            return configuration;

        var bestByCollapsedKey = new Dictionary<string, (string? Value, int ArrowDepth)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (originalKey, value) in entries)
        {
            if (value is not null && IsCollapseLeafKey(originalKey))
                throw new InvalidConfigurationsException(
                    $"The collapse key '{ConfigurationConstants.CollapseString}' at `{originalKey}` has the value `{value}`." +
                    $" The collapse key '{ConfigurationConstants.CollapseString}' must contain a dictionary or a list, not a value");

            if (value is null) continue;

            var (collapsedKey, arrowDepth) = CollapseKey(originalKey);
            if (!bestByCollapsedKey.TryGetValue(collapsedKey, out var existing) || arrowDepth < existing.ArrowDepth)
                bestByCollapsedKey[collapsedKey] = (value, arrowDepth);
        }

        var flattened = bestByCollapsedKey
            .Select(kvp => new KeyValuePair<string, string?>(kvp.Key, kvp.Value.Value))
            .ToList();
        return new ConfigurationBuilder().AddInMemoryCollection(flattened).Build();
    }

    private static bool IsCollapseLeafKey(string key) =>
        key == ConfigurationConstants.CollapseString ||
        key.EndsWith(ConfigurationConstants.PathSeparator + ConfigurationConstants.CollapseString, StringComparison.Ordinal);

    // YamlDotNet does not resolve YAML "<<:" merge keys; they leak through as literal "<<"
    // segments in IConfiguration paths. When multiple merge sources are listed under a single
    // "<<" they appear as positional indices, e.g. "Foo:<<:0:Bar" / "Foo:<<:1:Bar".
    // CollapseKey strips both forms and returns the original path with its arrow depth so the
    // caller can prefer the shallowest (most specific) value when two paths collapse to the same key.
    private static (string CollapsedKey, int ArrowDepth) CollapseKey(string key)
    {
        if (!key.Contains(ConfigurationConstants.CollapseString, StringComparison.Ordinal)) return (key, 0);

        var segments = key.Split(ConfigurationConstants.PathSeparator[0]);
        var kept = new List<string>(segments.Length);
        var arrowDepth = 0;
        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i] != ConfigurationConstants.CollapseString)
            {
                kept.Add(segments[i]);
                continue;
            }
            arrowDepth++;
            if (i + 1 < segments.Length && int.TryParse(segments[i + 1], out _)) i++;
        }
        return (string.Join(ConfigurationConstants.PathSeparator, kept), arrowDepth);
    }

}
