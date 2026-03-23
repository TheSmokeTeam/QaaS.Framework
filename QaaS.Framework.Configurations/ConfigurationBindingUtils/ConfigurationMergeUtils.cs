using System.Collections;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;

namespace QaaS.Framework.Configurations.ConfigurationBindingUtils;

/// <summary>
/// Provides partial-update merge helpers for configuration objects and <see cref="IConfiguration"/> instances.
/// </summary>
public static class ConfigurationMergeUtils
{
    private const System.Reflection.BindingFlags MergePropertyBindingFlags =
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic;

    private static readonly BinderOptions MergeBinderOptions = new()
    {
        ErrorOnUnknownConfiguration = true,
        BindNonPublicProperties = true
    };

    /// <summary>
    /// Merges a partial configuration object into an existing <see cref="IConfiguration"/> instance.
    /// Fields omitted from <paramref name="configurationObject"/> are preserved from
    /// <paramref name="configuration"/>. A field is treated as omitted when it still matches the
    /// default value produced by a fresh instance of the same configuration type.
    /// </summary>
    public static IConfiguration MergeConfigurationObjectIntoIConfiguration(this IConfiguration configuration,
        object? configurationObject)
    {
        var currentConfiguration = configuration.GetDictionaryFromConfiguration();
        var patchConfiguration = GetPatchDictionary(configurationObject);
        return MergeDictionaries(currentConfiguration, patchConfiguration).BindToDictionaryIConfiguration();
    }

    /// <summary>
    /// Merges a partial configuration object into an existing configuration instance.
    /// When the incoming configuration type differs from the existing one, the incoming configuration replaces it.
    /// Fields that still match a fresh default instance of the incoming configuration type are ignored.
    /// </summary>
    public static TConfiguration? MergeConfiguration<TConfiguration>(this TConfiguration? currentConfiguration,
        TConfiguration? newConfiguration)
    {
        if (newConfiguration == null)
        {
            return currentConfiguration;
        }

        if (currentConfiguration == null || currentConfiguration.GetType() != newConfiguration.GetType())
        {
            return newConfiguration;
        }

        var currentConfigurationDictionary = BuildCurrentDictionary(currentConfiguration);
        var patchConfigurationDictionary = GetPatchDictionary(newConfiguration);
        var mergedConfiguration = MergeDictionaries(currentConfigurationDictionary, patchConfigurationDictionary)
            .BindToDictionaryIConfiguration()
            .BindToObject(currentConfiguration.GetType(), MergeBinderOptions);

        return (TConfiguration)mergedConfiguration;
    }

    private static Dictionary<string, object?> BuildCurrentDictionary(object configurationObject)
    {
        var currentDictionary = DictionaryUtils.CreateConfigurationDictionary<object?>();
        foreach (var propertyInfo in GetConfigurationProperties(configurationObject.GetType()))
        {
            if (!propertyInfo.CanRead || propertyInfo.GetIndexParameters().Length != 0 ||
                !IsPatchableProperty(propertyInfo) ||
                !TryGetPropertyValue(propertyInfo, configurationObject, out var propertyValue))
            {
                continue;
            }

            currentDictionary[propertyInfo.Name] = ConvertCurrentValue(propertyInfo.PropertyType, propertyValue);
        }

        return currentDictionary;
    }

    private static Dictionary<string, object?> GetPatchDictionary(object? configurationObject)
    {
        var patchDictionary = DictionaryUtils.CreateConfigurationDictionary<object?>();
        if (configurationObject == null)
        {
            return patchDictionary;
        }

        object? defaultConfiguration;
        try
        {
            defaultConfiguration = configurationObject.GetType().CreateInstance();
        }
        catch
        {
            defaultConfiguration = null;
        }
        foreach (var propertyInfo in GetConfigurationProperties(configurationObject.GetType()))
        {
            if (!propertyInfo.CanRead || propertyInfo.GetIndexParameters().Length != 0 ||
                !IsPatchableProperty(propertyInfo))
            {
                continue;
            }

            if (!TryGetPropertyValue(propertyInfo, configurationObject, out var propertyValue))
            {
                continue;
            }

            var defaultPropertyValue = defaultConfiguration == null
                ? propertyInfo.PropertyType.GetDefaultValue()
                : TryGetPropertyValue(propertyInfo, defaultConfiguration, out var value)
                    ? value
                    : propertyInfo.PropertyType.GetDefaultValue();
            if (ShouldSkipPatchValue(propertyInfo.PropertyType, propertyValue, defaultPropertyValue))
            {
                continue;
            }

            patchDictionary[propertyInfo.Name] = ConvertPatchValue(propertyInfo.PropertyType, propertyValue);
        }

        return patchDictionary;
    }

    private static Dictionary<string, object?> MergeDictionaries(IDictionary<string, object?> currentConfiguration,
        IDictionary<string, object?> patchConfiguration)
    {
        var mergedConfiguration = CloneDictionary(currentConfiguration);
        foreach (var patchValuePair in patchConfiguration)
        {
            if (!mergedConfiguration.TryGetValue(patchValuePair.Key, out var currentValue))
            {
                mergedConfiguration[patchValuePair.Key] = CloneValue(patchValuePair.Value);
                continue;
            }

            mergedConfiguration[patchValuePair.Key] = MergeValues(currentValue, patchValuePair.Value);
        }

        return mergedConfiguration;
    }

    private static object? MergeValues(object? currentValue, object? patchValue)
    {
        if (patchValue == null)
        {
            return CloneValue(currentValue);
        }

        if (currentValue is IDictionary<string, object?> currentDictionary &&
            patchValue is IDictionary<string, object?> patchDictionary)
        {
            return MergeDictionaries(currentDictionary, patchDictionary);
        }

        if (patchValue is IList patchList)
        {
            return patchList.Count == 0 ? CloneValue(currentValue) : CloneValue(patchValue);
        }

        return CloneValue(patchValue);
    }

    private static object? ConvertPatchValue(Type propertyType, object? propertyValue)
    {
        if (propertyValue == null)
        {
            return null;
        }

        if (propertyValue is IConfiguration configuration)
        {
            return configuration.GetDictionaryFromConfiguration();
        }

        if (propertyValue is IDictionary dictionary)
        {
            return ConvertDictionary(dictionary);
        }

        if (propertyValue is IEnumerable enumerable && propertyType != typeof(string))
        {
            return ConvertList(enumerable);
        }

        return propertyType.IsClass && propertyType != typeof(string) ? GetPatchDictionary(propertyValue) : propertyValue;
    }

    private static object? ConvertCurrentValue(Type propertyType, object? propertyValue)
    {
        if (propertyValue == null)
        {
            return null;
        }

        if (propertyValue is IConfiguration configuration)
        {
            return configuration.GetDictionaryFromConfiguration();
        }

        if (propertyValue is IDictionary dictionary)
        {
            return ConvertCurrentDictionary(dictionary);
        }

        if (propertyValue is IEnumerable enumerable && propertyType != typeof(string))
        {
            return ConvertCurrentList(enumerable);
        }

        return propertyType.IsClass && propertyType != typeof(string)
            ? BuildCurrentDictionary(propertyValue)
            : propertyValue;
    }

    private static Dictionary<string, object?> ConvertDictionary(IDictionary dictionary)
    {
        var convertedDictionary = DictionaryUtils.CreateConfigurationDictionary<object?>();
        foreach (DictionaryEntry entry in dictionary)
        {
            var key = entry.Key.ToString()!;
            if (entry.Value == null)
            {
                continue;
            }

            convertedDictionary[key] = entry.Value switch
            {
                IConfiguration configuration => configuration.GetDictionaryFromConfiguration(),
                IDictionary nestedDictionary => ConvertDictionary(nestedDictionary),
                IEnumerable enumerable when entry.Value is not string => ConvertList(enumerable),
                _ => entry.Value.GetType().IsClass && entry.Value is not string
                    ? GetPatchDictionary(entry.Value)
                    : entry.Value
            };
        }

        return convertedDictionary;
    }

    private static List<object?> ConvertList(IEnumerable enumerable)
    {
        var convertedList = new List<object?>();
        foreach (var item in enumerable)
        {
            if (item == null)
            {
                continue;
            }

            convertedList.Add(item switch
            {
                IConfiguration configuration => configuration.GetDictionaryFromConfiguration(),
                IDictionary dictionary => ConvertDictionary(dictionary),
                IEnumerable nestedEnumerable when item is not string => ConvertList(nestedEnumerable),
                _ => item.GetType().IsClass && item is not string
                    ? GetPatchDictionary(item)
                    : item
            });
        }

        return convertedList;
    }

    private static Dictionary<string, object?> ConvertCurrentDictionary(IDictionary dictionary)
    {
        var convertedDictionary = DictionaryUtils.CreateConfigurationDictionary<object?>();
        foreach (DictionaryEntry entry in dictionary)
        {
            var key = entry.Key.ToString()!;
            convertedDictionary[key] = ConvertCurrentValue(entry.Value?.GetType() ?? typeof(object), entry.Value);
        }

        return convertedDictionary;
    }

    private static List<object?> ConvertCurrentList(IEnumerable enumerable)
    {
        var convertedList = new List<object?>();
        foreach (var item in enumerable)
        {
            convertedList.Add(ConvertCurrentValue(item?.GetType() ?? typeof(object), item));
        }

        return convertedList;
    }

    private static bool ShouldSkipPatchValue(Type propertyType, object? propertyValue, object? defaultPropertyValue)
    {
        if (propertyValue == null)
        {
            return true;
        }

        if (propertyValue is IConfiguration configuration)
        {
            return AreEquivalentValues(configuration.GetDictionaryFromConfiguration(),
                defaultPropertyValue is IConfiguration defaultConfiguration
                    ? defaultConfiguration.GetDictionaryFromConfiguration()
                    : defaultPropertyValue);
        }

        if (propertyValue is IDictionary dictionary)
        {
            return AreEquivalentValues(ConvertDictionary(dictionary),
                defaultPropertyValue is IDictionary defaultDictionary
                    ? ConvertDictionary(defaultDictionary)
                    : defaultPropertyValue);
        }

        if (propertyValue is IEnumerable enumerable && propertyType != typeof(string))
        {
            return AreEquivalentValues(ConvertList(enumerable),
                defaultPropertyValue is IEnumerable defaultEnumerable && defaultPropertyValue is not string
                    ? ConvertList(defaultEnumerable)
                    : defaultPropertyValue);
        }

        if (propertyType.IsClass && propertyType != typeof(string))
        {
            return AreEquivalentValues(GetPatchDictionary(propertyValue),
                defaultPropertyValue == null ? null : GetPatchDictionary(defaultPropertyValue));
        }

        return Equals(propertyValue, defaultPropertyValue);
    }

    private static Dictionary<string, object?> CloneDictionary(IDictionary<string, object?> sourceDictionary)
    {
        var clonedDictionary = DictionaryUtils.CreateConfigurationDictionary<object?>();
        foreach (var pair in sourceDictionary)
        {
            clonedDictionary[pair.Key] = CloneValue(pair.Value);
        }

        return clonedDictionary;
    }

    private static IEnumerable<System.Reflection.PropertyInfo> GetConfigurationProperties(Type type)
    {
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        for (var currentType = type; currentType != null && currentType != typeof(object);
             currentType = currentType.BaseType)
        {
            foreach (var property in currentType.GetProperties(MergePropertyBindingFlags |
                                                               System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (seenNames.Add(property.Name))
                {
                    yield return property;
                }
            }
        }
    }

    private static bool TryGetPropertyValue(System.Reflection.PropertyInfo propertyInfo, object instance,
        out object? value)
    {
        try
        {
            value = propertyInfo.GetValue(instance);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    private static bool IsPatchableProperty(System.Reflection.PropertyInfo propertyInfo)
    {
        return propertyInfo.CanWrite ||
               HasAutoPropertyBackingField(propertyInfo) ||
               propertyInfo.DeclaringType?.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false) == true;
    }

    private static bool HasAutoPropertyBackingField(System.Reflection.PropertyInfo propertyInfo)
    {
        var bindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        return propertyInfo.DeclaringType?.GetField($"<{propertyInfo.Name}>k__BackingField", bindingFlags) != null ||
               propertyInfo.DeclaringType?.GetField($"<{propertyInfo.Name}>i__Field", bindingFlags) != null;
    }

    private static bool AreEquivalentValues(object? leftValue, object? rightValue)
    {
        if (leftValue == null || rightValue == null)
        {
            return leftValue == null && rightValue == null;
        }

        if (leftValue is IDictionary<string, object?> leftDictionary &&
            rightValue is IDictionary<string, object?> rightDictionary)
        {
            return leftDictionary.Count == rightDictionary.Count &&
                   leftDictionary.All(pair => rightDictionary.TryGetValue(pair.Key, out var rightEntry) &&
                                             AreEquivalentValues(pair.Value, rightEntry));
        }

        if (leftValue is IList leftList && rightValue is IList rightList)
        {
            return leftList.Count == rightList.Count &&
                   leftList.Cast<object?>().Zip(rightList.Cast<object?>(), AreEquivalentValues).All(result => result);
        }

        return Equals(leftValue, rightValue);
    }

    private static object? CloneValue(object? value)
    {
        return value switch
        {
            null => null,
            IDictionary<string, object?> dictionary => CloneDictionary(dictionary),
            IList list => list.Cast<object?>().Select(CloneValue).ToList(),
            _ => value
        };
    }
}
