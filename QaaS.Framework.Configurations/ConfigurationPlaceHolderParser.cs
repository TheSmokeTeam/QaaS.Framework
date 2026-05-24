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
    private bool _configurationReplaced;

    /// <summary>
    /// Resolves all the placeholders in the configuration and returns the resolved configuration.
    /// </summary>
    public IConfiguration ResolvePlaceholders()
    {
        var pathsContainingPlaceholders = RebuildPathIndexAndCollectPlaceholders();

        // Fixed-point loop: a placeholder value can itself contain placeholders that resolve to
        // further placeholders, so keep iterating until a full pass produces no new substitutions.
        // _modificationCount is incremented by SetValue / CopyConfigurationsByPath when anything changes.
        // If CopyConfigurationsByPath replaces the entire tree (object-valued placeholder copies a
        // subtree that itself contains placeholders), the new paths weren't in our snapshot, so we
        // re-collect them on the next pass — the _configurationReplaced flag drives that re-scan.
        int modificationCountAtPassStart;
        do
        {
            modificationCountAtPassStart = _modificationCount;
            foreach (var pathContainingPlaceholder in pathsContainingPlaceholders)
            {
                var currentValueAtPath = configuration[pathContainingPlaceholder];
                if (currentValueAtPath is not null && currentValueAtPath.Contains(Prefix, StringComparison.Ordinal))
                    ResolvePlaceholderValue(pathContainingPlaceholder);
            }
            if (_configurationReplaced)
            {
                pathsContainingPlaceholders = RebuildPathIndexAndCollectPlaceholders();
                _configurationReplaced = false;
            }
        } while (_modificationCount != modificationCountAtPassStart);

        return configuration;
    }

    // Single pass over the config tree: rebuilds the existing-path / parent-path indices used by
    // IsConfigurationSectionString + GetObjectFromConfiguration, and returns the leaf paths whose
    // value contains a placeholder so the resolver can iterate just those instead of the whole tree.
    private List<string> RebuildPathIndexAndCollectPlaceholders()
    {
        _existingPaths.Clear();
        _parentPaths.Clear();
        var pathsContainingPlaceholders = new List<string>();
        foreach (var configurationEntry in configuration.AsEnumerable())
        {
            _existingPaths.Add(configurationEntry.Key);
            var ancestorPath = configurationEntry.Key;
            int lastPathSeparatorIndex;
            while ((lastPathSeparatorIndex = ancestorPath.LastIndexOf(ConfigurationConstants.PathSeparator[0])) > 0)
            {
                ancestorPath = ancestorPath[..lastPathSeparatorIndex];
                if (!_parentPaths.Add(ancestorPath)) break;
            }
            if (configurationEntry.Value is { } entryValue && entryValue.Contains(Prefix, StringComparison.Ordinal))
                pathsContainingPlaceholders.Add(configurationEntry.Key);
        }
        return pathsContainingPlaceholders;
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
                // Recursively resolves the placeholder value path. Wrapped in try/finally so an
                // exception thrown during recursion or substring-validation (e.g. object-placeholder
                // used as substring) does not leave a stale entry in _resolutionStack that would
                // make a subsequent valid resolve falsely look circular on the same parser instance.
                _resolutionStack.Add(placeholderValuePath);
                try
                {
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
                        break;
                    }

                    // If the placeholder value is a string, replaces the placeholder with the string value and continues to find another placeholders.
                    sectionValue = sectionValue.Substring(0, placeholderStartIndex) + resolvedSection.Value +
                                   sectionValue.Substring(end + 1);
                    currentSection.Value = sectionValue;
                    SetValue(path, sectionValue);
                    lastEnd = placeholderStartIndex + resolvedSection.Value!.Length; // Section is tested not to be null at IsConfigurationSectionString
                }
                finally
                {
                    _resolutionStack.Remove(placeholderValuePath);
                }
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
        // Rebuild the path indices immediately so IsConfigurationSectionString and
        // GetObjectFromConfiguration see the new tree for the remainder of this pass.
        // Signal to the outer fixed-point loop that the placeholder snapshot is stale and
        // must be re-collected before the next iteration; otherwise new placeholders that
        // were copied along with the subtree would never get resolved.
        _ = RebuildPathIndexAndCollectPlaceholders();
        _configurationReplaced = true;
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