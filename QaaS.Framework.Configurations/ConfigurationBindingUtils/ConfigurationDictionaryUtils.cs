using System.Collections;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations;

namespace QaaS.Framework.Configurations.ConfigurationBindingUtils;

/// <summary>
/// Utility methods for dictionaries
/// </summary>
internal static class DictionaryUtils
{
    /// <summary>
    /// Flattens a dictionary of object to a dictionary of strings
    /// </summary>
    /// <remarks>The method only supports flattening IConfiguration based dictionaries</remarks>
    /// <param name="dict">The dictionary to flatten</param>
    /// <param name="result">The dictionary to put the flattened dict in</param>
    /// <param name="parentKey">The parent key of the key, used to concat the key path</param>
    /// <returns>The flattened dictionary</returns>
    internal static IDictionary<string, string?> GetInMemoryCollectionFromDictionary(
        this IDictionary<string, object?> dict, Dictionary<string, string?> result, string parentKey = "")
    {
        foreach (var keyValuePair in dict)
        {
            var flattenKey = string.IsNullOrEmpty(parentKey)
                ? keyValuePair.Key
                : $"{parentKey}{ConfigurationConstants.PathSeparator}{keyValuePair.Key}";
            switch (keyValuePair.Value)
            {
                case IConvertible or null:
                    result[flattenKey] = keyValuePair.Value?.ToString() ?? null; break;
                case IConfiguration nestedConfig:
                    var nestedConfigFlattened = nestedConfig.GetDictionaryFromConfiguration()
                        .GetInMemoryCollectionFromDictionary(result, flattenKey);
                    foreach (var nestedKvp in nestedConfigFlattened)
                        result[nestedKvp.Key] = nestedKvp.Value;
                    break;
                case IDictionary nestedDict:
                    var nestedFlattened = new Dictionary<string, object?>(nestedDict as IDictionary<string, object?> ??
                                                                          new Dictionary<string, object?>())
                        .GetInMemoryCollectionFromDictionary(result, flattenKey);
                    foreach (var nestedKvp in nestedFlattened)
                        result[nestedKvp.Key] = nestedKvp.Value;
                    break;
                case IList nestedList:
                    GetInMemoryCollectionFromList(nestedList, flattenKey, result);
                    break;
                default:
                    result.GetInMemoryCollectionFromObject(keyValuePair.Value, flattenKey); break;
            }
        }

        return result;
    }

    /// <summary>
    /// Flattens a list of object to a dictionary of strings
    /// </summary>
    /// <remarks>The method only supports flattening IConfiguration based lists</remarks>
    /// <param name="nestedList">The list to flatten</param>
    /// <param name="result">The dictionary to put the flattened list in</param>
    /// <param name="parentKey">The parent key of the key, used to concat the key path</param>
    internal static void GetInMemoryCollectionFromList(this IList nestedList, string? parentKey,
        Dictionary<string, string?> result)
    {
        for (var listIndex = 0; listIndex < nestedList.Count; listIndex++)
        {
            var listItemKey = $"{parentKey}{ConfigurationConstants.PathSeparator}{listIndex}";
            var listItemValue = nestedList[listIndex];
            switch (listItemValue)
            {
                case IConvertible or null:
                    result[listItemKey] = listItemValue?.ToString() ?? string.Empty; break;
                case IConfiguration nestedConfig:
                    nestedConfig.GetDictionaryFromConfiguration()
                        .GetInMemoryCollectionFromDictionary(result, listItemKey); break;
                case IDictionary nestedDict:
                    new Dictionary<string, object?>(nestedDict as IDictionary<string, object?> ??
                                                    new Dictionary<string, object?>())
                        .GetInMemoryCollectionFromDictionary(result, listItemKey); break;
                case IList listItemList:
                    GetInMemoryCollectionFromList(listItemList, listItemKey, result);
                    break;
                default:
                    result.GetInMemoryCollectionFromObject(listItemValue, listItemKey); break;
            }
        }
    }

    /// <summary>
    /// Binds a dictionary of object to an IConfiguration instance
    /// </summary>
    internal static IConfiguration BindToDictionaryIConfiguration(
        this IDictionary<string, object?> sourceDictionary)
    {
        var configurationKeyValuePairs = sourceDictionary.GetInMemoryCollectionFromDictionary(
            new Dictionary<string, string?>());
        var instance = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationKeyValuePairs).Build();
        return instance;
    }

    /// <summary>
    /// Returns true if the type is a dictionary
    /// </summary>
    internal static bool IsTypeDictionary(this Type type) =>
        type == typeof(IDictionary) ||
        typeof(IDictionary).IsAssignableFrom(type);

    /// <summary>
    /// Returns true if the type is a key value pair
    /// </summary>
    internal static bool IsTypeKeyValuePair(this Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>);

    /// <summary>
    /// Convert dictionary to list according to the keys numeric values, and if not numeric by the dictionary order 
    /// </summary>
    internal static IList ConvertDictionaryToListAccordingToKeys(this IDictionary<string, object> dictionary)
    {
        try
        {
            return dictionary.OrderBy(kvp => int.Parse(kvp.Key)).Select(kvp => kvp.Value).ToList();
        }
        catch
        {
            return dictionary.Values.ToList();
        }
    }
}