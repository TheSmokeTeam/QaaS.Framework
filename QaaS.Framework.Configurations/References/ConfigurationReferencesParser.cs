using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations.CustomExceptions;
using QaaS.Framework.Configurations.ConfigurationBuilderExtensions;

namespace QaaS.Framework.Configurations.References;

/// <summary>
/// Responsible for any reference related parsing of the <see cref="IConfiguration"/>
/// </summary>
public static class ConfigurationReferencesParser
{
    /// <summary>
    /// Resolves all references in configuration according to loaded reference configs and given
    /// <see cref="referenceResolutionPaths"/>
    /// </summary>
    /// <param name="builtConfiguration"> The built configuration loaded to resolve references on top of </param>
    /// <param name="referenceConfigs"> The configurations of all the references to resolve in the
    /// given built configuration </param>
    /// <param name="referenceResolutionPaths"> A list containing a path to a list in the configuration to resolve
    /// references for </param>
    /// <param name="uniqueIdPathRegexes"> A list of regexes that describe the paths to unique id fields in the given
    /// referencess configurations that should have the reference replace keyword added to their value as a prefix </param>
    /// <param name="resolveReferencesWithEnvironmentVariables"> Whether to resolve the reference configurations
    /// with environment variables resolution or not </param>
    /// <returns> The built configuration with all of its references resolved </returns>
    /// <exception cref="InvalidConfigurationsException">
    /// While loading the references invalidity in the configuration was observed </exception>
    public static IConfiguration ResolveReferencesInConfiguration(this IConfiguration builtConfiguration,
        ICollection<ReferenceConfig>? referenceConfigs,
        IList<string>? referenceResolutionPaths,
        IList<string>? uniqueIdPathRegexes, bool resolveReferencesWithEnvironmentVariables)
    {
        if ((referenceResolutionPaths?.Count ?? 0) == 0 || (referenceConfigs?.Count ?? 0) == 0)
            return builtConfiguration;
        
        foreach (var referenceConfig in referenceConfigs ?? Enumerable.Empty<ReferenceConfig>())
        {
            var referenceLoadedConfiguration = BuildReferencesConfiguration(referenceConfig,
                resolveReferencesWithEnvironmentVariables);
            foreach (var referencePath in 
                     referenceResolutionPaths ?? Enumerable.Empty<string>())
            {
                var referenceList = referenceLoadedConfiguration.GetSection(referencePath)
                    .GetChildren().ToArray();
                var referenceListItems = referenceList
                    .SelectMany(config=> config.AsEnumerable())
                    // Handle unique Id path regexes in references
                    .Select(itemPair => (uniqueIdPathRegexes ?? Enumerable.Empty<string>())
                        .Any(pathRegex => Regex.IsMatch(itemPair.Key, pathRegex))
                        ? new KeyValuePair<string, string?>(itemPair.Key,
                            referenceConfig.ReferenceReplaceKeyword + itemPair.Value)
                        : new KeyValuePair<string, string?>(itemPair.Key, itemPair.Value))
                    .ToArray();
                
                var existingConfigList = builtConfiguration.GetSection(referencePath)
                    .GetChildren().ToArray();
                var existingConfigListItems = existingConfigList
                    .SelectMany(config=> config.AsEnumerable()).ToArray();

                var listIndexPattern = $@"(?<={referencePath + ConfigurationConstants.PathSeparator})\d+";
                    
                var listWithReferencedItems = 
                    ResolveReferenceWithReplaceKeyword(referenceConfig, existingConfigListItems, referenceListItems,
                        referenceList, referencePath, listIndexPattern) 
                    ?? existingConfigListItems;
                
                var allConfigurationExceptListInReferencePath = builtConfiguration.AsEnumerable()
                    .Where(pair => !pair.Key.StartsWith(referencePath));
                builtConfiguration = new ConfigurationBuilder()
                    .AddInMemoryCollection(allConfigurationExceptListInReferencePath)
                    .AddInMemoryCollection(listWithReferencedItems).Build();
            }
        }
        return builtConfiguration;
    }

    /// <summary>
    /// Injects the reference to a list with a replace keyword, injects it instead of the replace keyword
    /// </summary>
    /// <returns> The list after resolving the reference as an in memory collection,
    /// if no replace keyword was found returns null </returns>
    private static IEnumerable<KeyValuePair<string,string?>>? ResolveReferenceWithReplaceKeyword(
        ReferenceConfig referenceConfig,
        KeyValuePair<string,string?>[] existingConfigListItems,
        KeyValuePair<string,string?>[] referenceListItems,
        IConfigurationSection[] referenceList,
        string referencePath,
        string listIndexPattern)
    {
        var configListItemWithReferenceReplaceKeyword = existingConfigListItems.Where(
            existingConfigItem =>
                existingConfigItem.Value == referenceConfig.ReferenceReplaceKeyword).ToArray();
        
        // Check only 1 Replace KeyWord is present in configuration list
        switch (configListItemWithReferenceReplaceKeyword.Length)
        {
            case > 1:
                throw new InvalidConfigurationsException(
                    $"Found more than 1 instance of the replace keyword" +
                    $" `{referenceConfig.ReferenceReplaceKeyword}` in list at path `{referencePath}`");
            case < 1:
                return null;
        }

        var existingConfigItem = configListItemWithReferenceReplaceKeyword.First();
        var replaceKeywordIndex = int.Parse(Regex.Match(existingConfigItem.Key, listIndexPattern).Value);
        
       // Move existing list items to make place for the reference list items to be inserted 
        var shiftedExistingConfigListItems = existingConfigListItems
            // Remove replaced keyword from list
            .Where(pair => int.Parse(Regex.Match(pair.Key, listIndexPattern).Value) != replaceKeywordIndex)
            .Select(pair =>
            {
                var currentIndexInList = int.Parse(Regex.Match(pair.Key, listIndexPattern).Value);
                if (currentIndexInList < replaceKeywordIndex) return pair;
                var key = Regex.Replace(pair.Key, listIndexPattern, match =>
                    // Current index minus the replace key word (which takes 1 list item slot)
                    // plus the length of all added references instead of the keyword
                    (int.Parse(match.Value) - 1 + referenceList.Length)
                    .ToString());
                return new KeyValuePair<string, string?>(key, pair.Value);
            });
        
        // Insert reference list items in place of the replace keyword
        var additionalReferenceItems = referenceListItems
            .Select(pair => IncrementReferenceConfigurationListItemIndex(pair, listIndexPattern, replaceKeywordIndex));
        
        return shiftedExistingConfigListItems.Concat(additionalReferenceItems);
    }

    /// <summary>
    /// increment the given reference configuration list item (pair's) index
    /// </summary>
    private static KeyValuePair<string, string?> IncrementReferenceConfigurationListItemIndex(
        KeyValuePair<string, string?> pair, string listIndexPattern, int indexIncrease) =>
         new(key: Regex.Replace(pair.Key, listIndexPattern, match =>
                (int.Parse(match.Value) + indexIncrease).ToString()),
            value: pair.Value);

    /// <summary>
    /// Builds a references configuration from a reference config
    /// </summary>
    private static IConfiguration BuildReferencesConfiguration(ReferenceConfig referenceConfig, 
        bool resolveReferencesWithEnvironmentVariables)
    {
        var referenceConfigurationBuilder = new ConfigurationBuilder();
        foreach (var path in referenceConfig.ReferenceFilesPaths ?? Enumerable.Empty<string>())
            referenceConfigurationBuilder.AddYaml(path);
        return referenceConfigurationBuilder.EnrichedBuild(resolveReferencesWithEnvironmentVariables);
    }
}