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

    /// <summary>
    /// Resolves all the placeholders in the configuration and returns the resolved configuration.
    /// </summary>
    public IConfiguration ResolvePlaceholders()
    {
        List<KeyValuePair<string, string?>> previousConfigurationKeys;
        List<KeyValuePair<string, string?>> configurationKeys;
        do
        {
            previousConfigurationKeys = configuration.AsEnumerable().ToList();

            ResolveSection(configuration.GetChildren());
            configurationKeys = configuration.AsEnumerable().ToList();
        } while (!configurationKeys.SequenceEqual(previousConfigurationKeys));

        return configuration;
    }

    private void ResolveSection(IEnumerable<IConfigurationSection> sections)
    {
        foreach (var section in sections)
        {
            if (IsConfigurationSectionString(section))
            {
                ResolvePlaceholderValue(section.Path);
            }
            else
            {
                ResolveSection(section.GetChildren());
            }
        }
    }

    /// <summary>
    /// Resolves the place holder for the given paths, and all the dependent placeholders recursively
    /// </summary>
    /// <param name="path">The path to the placeholder</param>
    /// <returns>The <see cref="IConfigurationSection"/> of the resolved placeholder</returns>
    private IConfigurationSection ResolvePlaceholderValue(string path)
    {
        var currentSection = GetObjectFromConfiguration(path);
        var lastEnd = 0;

        while (IsConfigurationSectionString(currentSection))
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
                configuration[path] = sectionValue;
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
                configuration[path] = sectionValue;
                _resolutionStack.Remove(placeholderValuePath);
                lastEnd = placeholderStartIndex + resolvedSection.Value!.Length; // Section is tested not to be null at IsConfigurationSectionString
            }

        }

        return currentSection;
    }

    private static bool IsConfigurationSectionString(IConfigurationSection section)
    {
        return section.GetChildren().ToList().Count == 0 && section.Value != null;
    }

    /// <summary>
    /// Gets the configuration object from the path
    /// </summary>
    private IConfigurationSection GetObjectFromConfiguration(string path)
    {
        var paths = path.Split(ConfigurationConstants.PathSeparator);
        var currentConfiguration = configuration;
        var currentSection = currentConfiguration.GetChildren().FirstOrDefault(child => child.Key == paths[0]);
        currentSection = paths.Skip(1).Aggregate(currentSection,
            (current, key) => current?.GetChildren().FirstOrDefault(child => child.Key == key));

        return currentSection!;
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