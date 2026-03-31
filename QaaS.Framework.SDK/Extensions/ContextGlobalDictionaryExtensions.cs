using Microsoft.Extensions.Configuration;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.ExecutionObjects;

namespace QaaS.Framework.SDK.Extensions;

/// <summary>
/// Helpers for projecting configuration data into the runtime global dictionary.
/// </summary>
public static class ContextGlobalDictionaryExtensions
{
    /// <summary>
    /// Loads the requested configuration section into the context global dictionary.
    /// Use <c>"variables"</c> as the section path to project the root variables section
    /// into runtime state without relying on a dedicated Variables API.
    /// </summary>
    public static void LoadConfigurationSectionIntoGlobalDictionary<TExecutionData>(
        this BaseContext<TExecutionData> context,
        string configurationSectionPath,
        List<string>? destinationPath = null)
        where TExecutionData : class, IExecutionData, new()
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationSectionPath);

        var section = context.RootConfiguration.GetSection(configurationSectionPath);
        if (!section.Exists())
            throw new KeyNotFoundException(
                $"Configuration section '{configurationSectionPath}' was not found on the current context.");

        var path = destinationPath is { Count: > 0 }
            ? destinationPath
            : configurationSectionPath
                .Split(ConfigurationPath.KeyDelimiter, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

        if (path.Count == 0)
            throw new ArgumentException("Destination path cannot be empty.", nameof(destinationPath));

        context.InsertValueIntoGlobalDictionary(path, ConvertConfigurationSectionToObject(section));
    }

    private static object? ConvertConfigurationSectionToObject(IConfigurationSection section)
    {
        var children = section.GetChildren().ToList();
        if (children.Count == 0)
            return section.Value;

        var dictionary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in children)
            dictionary[child.Key] = ConvertConfigurationSectionToObject(child);

        return dictionary;
    }
}
