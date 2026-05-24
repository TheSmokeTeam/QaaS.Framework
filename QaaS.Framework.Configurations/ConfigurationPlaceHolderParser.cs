using Microsoft.Extensions.Configuration;

namespace QaaS.Framework.Configurations;

/// <summary>
/// Class that contains functionality for parsing the Placeholder values in a configuration
/// </summary>
public class ConfigurationPlaceholderParser(IConfiguration configuration)
{
    private const string Prefix = "${";
    private const string Suffix = "}";
    private const string NullSeparator = "??";
    private const char OpenCurlyBracket = '{';
    private const char CloseCurlyBracket = '}';

    private readonly HashSet<string> _resolutionStack = new();
    private readonly HashSet<string> _existingPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _parentPaths = new(StringComparer.OrdinalIgnoreCase);
    private int _modificationCount;

    /// <summary>
    /// Resolves all the placeholders in the configuration and returns the resolved configuration.
    /// </summary>
    public IConfiguration ResolvePlaceholders()
    {
        var pathsWithPlaceholders = RebuildPathIndexAndCollectPlaceholders();

        // Fixed-point loop: a placeholder value can itself contain placeholders that resolve to
        // further placeholders, so keep iterating until a full pass produces no new substitutions.
        // _modificationCount is incremented by SetValue / CopyConfigurationsByPath when anything changes.
        int previousMods;
        do
        {
            previousMods = _modificationCount;
            foreach (var path in pathsWithPlaceholders)
            {
                var value = configuration[path];
                if (value is not null && value.Contains(Prefix, StringComparison.Ordinal))
                    ResolvePlaceholderValue(path);
            }
        } while (_modificationCount != previousMods);

        return configuration;
    }

    // Single pass over the config tree: rebuilds the existing-path / parent-path indices used by
    // IsConfigurationSectionString + GetObjectFromConfiguration, and returns the leaf paths whose
    // value contains a placeholder so the resolver can iterate just those instead of the whole tree.
    private List<string> RebuildPathIndexAndCollectPlaceholders()
    {
        _existingPaths.Clear();
        _parentPaths.Clear();
        var pathsWithPlaceholders = new List<string>();
        foreach (var kvp in configuration.AsEnumerable())
        {
            _existingPaths.Add(kvp.Key);
            var ancestor = kvp.Key;
            int separator;
            while ((separator = ancestor.LastIndexOf(ConfigurationConstants.PathSeparator[0])) > 0)
            {
                ancestor = ancestor[..separator];
                if (!_parentPaths.Add(ancestor)) break;
            }
            if (kvp.Value is { } v && v.Contains(Prefix, StringComparison.Ordinal))
                pathsWithPlaceholders.Add(kvp.Key);
        }
        return pathsWithPlaceholders;
    }

    private void SetValue(string path, string? value)
    {
        configuration[path] = value;
        _modificationCount++;
    }

    /// <summary>
    /// Resolves the place holder for the given paths, and all the dependent placeholders recursively
    /// </summary>
    /// <param name="path">The path to the placeholder</param>
    /// <returns>The <see cref="IConfigurationSection"/> of the resolved placeholder</returns>
    private IConfigurationSection ResolvePlaceholderValue(string path)
    {
        var currentSection = GetObjectFromConfiguration(path);
        if (currentSection is null || !IsConfigurationSectionString(currentSection)) return currentSection!;
        var lastEnd = 0;

        while (currentSection.Value is not null)
        {
            var sectionValue = currentSection.Value;
            var placeholderStartIndex = sectionValue?.IndexOf(Prefix, lastEnd, StringComparison.Ordinal) ?? -1;
            if (placeholderStartIndex is -1) break; // If no Prefix for a placeholder was found, break.

            var end = FindClosingBracket(sectionValue!, placeholderStartIndex + 2);
            if (end == -1) break; // Continues only if the section has a string value containing placeholder.

            // Finds the placeholder value path and default value.
            var placeholder = sectionValue!.Substring(placeholderStartIndex + 2, end - placeholderStartIndex - 2);
            var placeholderParts = placeholder.Split(NullSeparator, 2);
            var placeholderValuePath = placeholderParts[0].Trim();
            var defaultValue = placeholderParts.Length > 1 ? placeholderParts[1].Trim() : null;

            if (_resolutionStack.Contains(placeholderValuePath))
                throw new InvalidOperationException("Circular placeholder reference detected in configuration at: " +
                                                    path);

            var placeholderResolvedConfigurationObject = GetObjectFromConfiguration(placeholderValuePath);
            if (placeholderResolvedConfigurationObject == null && defaultValue == null) break;

            // If placeholder was not found but there is a default value, sets the default value to be the placeholder value and call the function again.
            if (placeholderResolvedConfigurationObject == null)
            {
                sectionValue = sectionValue.Substring(0, placeholderStartIndex) + defaultValue +
                               sectionValue.Substring(end + 1);
                SetValue(path, sectionValue);
                currentSection = ResolvePlaceholderValue(path);
            }
            else
            {
                // Recursively resolves the placeholder value path. 
                _resolutionStack.Add(placeholderValuePath);
                var resolvedSection = ResolvePlaceholderValue(placeholderValuePath);
                var hasLeadingTrailingCharsFromPlaceholder = !(sectionValue.StartsWith(Prefix) &&
                                                               sectionValue.EndsWith(Suffix) && sectionValue.Skip(end)
                                                                   .Any(chr => chr == CloseCurlyBracket));

                if (!IsConfigurationSectionString(resolvedSection) && hasLeadingTrailingCharsFromPlaceholder)
                    throw new InvalidOperationException(
                        "Placeholder reference to an object but is a substring value at: " + path);

                if (!IsConfigurationSectionString(resolvedSection))
                {
                    CopyConfigurationsByPath(placeholderValuePath, path);
                    currentSection = resolvedSection;
                    _resolutionStack.Remove(placeholderValuePath);
                    break;
                }

                // If the placeholder value is a string, replaces the placeholder with the string value and continues to find another placeholders.
                sectionValue = sectionValue.Substring(0, placeholderStartIndex) + resolvedSection.Value +
                               sectionValue.Substring(end + 1);
                currentSection.Value = sectionValue;
                SetValue(path, sectionValue);
                _resolutionStack.Remove(placeholderValuePath);
                lastEnd = placeholderStartIndex + resolvedSection.Value!.Length; // Section is tested not to be null at IsConfigurationSectionString
            }

        }

        return currentSection;
    }

    private bool IsConfigurationSectionString(IConfigurationSection section)
    {
        return section.Value != null && !_parentPaths.Contains(section.Path);
    }

    /// <summary>
    /// Gets the configuration object from the path
    /// </summary>
    private IConfigurationSection GetObjectFromConfiguration(string path)
    {
        return _existingPaths.Contains(path) ? configuration.GetSection(path) : null!;
    }

    /// <summary>
    /// Copies the configuration object from the source path to destination path
    /// </summary>
    private void CopyConfigurationsByPath(string sourcePath, string destinationPath)
    {
        var configKeys = configuration.AsEnumerable()
            .Where(kvp => !(kvp.Key.Equals(destinationPath) || kvp.Key.StartsWith(destinationPath + ConfigurationConstants.PathSeparator))).ToList();
        var newConfigKeys = configKeys.Where(kvp => kvp.Key.Equals(sourcePath) ||kvp.Key.StartsWith(sourcePath + ConfigurationConstants.PathSeparator))
            .Select(kvp => new KeyValuePair<string, string?>(kvp.Key.Replace(sourcePath, destinationPath), kvp.Value))
            .ToList();
        configKeys = configKeys.Concat(newConfigKeys).ToList();
        configuration = new ConfigurationBuilder().AddInMemoryCollection(configKeys).Build();
        _modificationCount++;
        // Rebuild index after the configuration tree was replaced; we don't need the returned
        // placeholder list here — the outer fixed-point loop already has its snapshot.
        _ = RebuildPathIndexAndCollectPlaceholders();
    }

    private static int FindClosingBracket(string str, int startIndex)
    {
        var depth = 1;
        for (var currentIndex = startIndex + 1; currentIndex < str.Length; currentIndex++)
        {
            if (str[currentIndex] == OpenCurlyBracket) depth++;
            if (str[currentIndex] == CloseCurlyBracket) depth--;
            if (depth == 0) return currentIndex;
        }

        return -1;
    }
}