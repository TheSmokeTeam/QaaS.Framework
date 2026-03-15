using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Configurations;
using QaaS.Framework.Configurations.CustomExceptions;
using QaaS.Framework.Configurations.ConfigurationBindingUtils;
using QaaS.Framework.Configurations.ConfigurationBuilderExtensions;
using YamlDotNet.Serialization;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace QaaS.Framework.Configurations;

/// <summary>
/// All utility functions for handling configurations 
/// </summary>
public static class ConfigurationUtils
{
    /// <summary>
    /// Serializes <see cref="IConfiguration"/> object to Yaml string by a specific given order of
    /// its content's sections if given - else return default serialize result.
    /// </summary>
    /// <param name="configuration"><see cref="IConfiguration"/> object to perform serialization onto and output as
    /// Yaml string</param>
    /// <param name="configurationSectionNames">Names of sections inside the configuration to serialize
    /// into the string result, by their given order</param>
    /// <returns></returns>
    public static string BuildConfigurationAsYaml(this IConfiguration configuration,
        List<string>? configurationSectionNames = null)
    {
        var yamlSerializer = new SerializerBuilder().WithIndentedSequences().Build();
        var stringBuilder = new StringBuilder();

        IEnumerable<KeyValuePair<string, object?>> configurationSections =
            configuration.GetDictionaryFromConfiguration();
        if (configurationSectionNames != null)
            configurationSections = configurationSections
                .Where(section => configurationSectionNames.Contains(section.Key))
                .OrderBy(section => configurationSectionNames.IndexOf(section.Key));

        foreach (var configurationSection in configurationSections)
        {
            var configurationDict = new[] { configurationSection }.ToDictionary();
            stringBuilder.Append($"{yamlSerializer.Serialize(configurationDict)}\r\n");
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Builds from given configuration c# object an <see cref="IDictionary{TKey,TValue}"/> inMemoryCollection
    /// of string paths to string values.
    /// </summary>
    /// <param name="configurationObject">The configuration object to parse</param>
    /// <param name="bindingAttr"><seealso cref="BindingFlags"/></param>
    public static Dictionary<string, string?> GetInMemoryCollectionFromObject(object? configurationObject,
        BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public)
    {
        Dictionary<string, string?> inMemoryCollection = [];
        if (configurationObject == null)
            return inMemoryCollection;

        return inMemoryCollection.GetInMemoryCollectionFromObject(configurationObject,
            bindingAttr: bindingAttr);
    }

    internal static Dictionary<string, string?> GetInMemoryCollectionFromObject(
        this Dictionary<string, string?> inMemoryCollection, object value, string? parentKey = null,
        BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public)
    {
        if (!value.GetType().IsClass)
            throw new ArgumentException(
                $"Can't parse Object valued - {value} of type {value.GetType()} into a {nameof(inMemoryCollection)} " +
                $"dictionary. Object must be a serializable class object. Path - {parentKey}");
        var properties = value.GetType().GetProperties(bindingAttr);

        foreach (var propertyInfo in properties)
        {
            if (propertyInfo.GetIndexParameters().Length > 0)
            {
                continue;
            }

            string key = parentKey != null
                ? $"{parentKey}{ConfigurationConstants.PathSeparator}{propertyInfo.Name}"
                : propertyInfo.Name;
            object? nestedValue = propertyInfo.GetValue(value);

            switch (nestedValue)
            {
                case IConvertible or null:
                    inMemoryCollection[key] = nestedValue?.ToString() ?? null; break;
                case IConfiguration nestedConfig:
                    nestedConfig.GetDictionaryFromConfiguration()
                        .GetInMemoryCollectionFromDictionary(inMemoryCollection, key); break;
                case IDictionary nestedDict:
                    DictionaryUtils.ToStringObjectDictionary(nestedDict)
                        .GetInMemoryCollectionFromDictionary(inMemoryCollection, key); break;
                case IEnumerable nestedCollection:
                    nestedCollection.Cast<object?>().ToList()
                        .GetInMemoryCollectionFromList(key, inMemoryCollection); break;
                default:
                    inMemoryCollection.GetInMemoryCollectionFromObject(nestedValue, key, bindingAttr); break;
            }
        }

        return inMemoryCollection;
    }

    /// <summary>
    /// Load <see cref="IConfiguration"/> to a c# object and validate it
    /// </summary>
    /// <param name="configuration"> Configurations to load into a c# object and validate </param>
    /// <param name="binderOptions"> The options of the binder that binds the configurations to the
    /// configurations object</param>
    /// <param name="logger"> Logger to use to log any warnings or errors that may occur while loading or validating
    /// configurations object </param>
    /// <typeparam name="TConfiguration"> The type of the c# object to load the configurations to </typeparam>
    /// <returns> The loaded configuration C# object </returns>
    /// <exception cref="InvalidConfigurationsException"> Thrown when configurations given are not valid
    /// according to data annotations on given configuration C# object </exception>
    public static TConfiguration LoadAndValidateConfiguration<TConfiguration>(this IConfiguration configuration,
        BinderOptions? binderOptions = null, ILogger? logger = null)
        where TConfiguration : new()
    {
        binderOptions ??= new BinderOptions
        {
            ErrorOnUnknownConfiguration = false,
            BindNonPublicProperties = false
        };
        // Load configuration to c# object, and validate them
        var configurationObject = configuration.BindToObject<TConfiguration>(binderOptions, logger);

        // Validate loaded configuration
        var validationResults = new List<ValidationResult>();
        var valid = ValidationUtils.TryValidateObjectRecursive(configurationObject, validationResults);

        if (!valid)
            throw new InvalidConfigurationsException(
                "Given configurations are not valid. The validation results are: \n- " +
                $"{string.Join("\n- ", validationResults.Select(result => result.ErrorMessage))}");

        return configurationObject ?? new TConfiguration();
    }

    /// <summary>
    /// Builds IConfiguration from configuration builder while adding all
    /// parameterless configuration resolution extensions to the build process
    /// </summary>
    public static IConfiguration EnrichedBuild(this IConfigurationBuilder configurationBuilder,
        bool addEnvironmentVariables) =>
        addEnvironmentVariables
            ? configurationBuilder.AddEnvironmentVariables().AddPlaceholderResolver().Build()
                .CollapseShiftLeftArrowsInConfiguration()
            : configurationBuilder.AddPlaceholderResolver().Build()
                .CollapseShiftLeftArrowsInConfiguration();

    /// <summary>
    /// Converts IConfiguration object to a c# object of given type and validates the object according to
    /// DataAnnotations
    /// </summary>
    /// <param name="source">ICConfiguration to convert</param>
    /// <param name="binderOptions"> The options of the binder that binds the configurations to the
    /// configurations object</param>
    /// <param name="logger">Logger to use to log any warnings or errors that may occur while loading or validating
    /// configurations object </param>
    /// <typeparam name="T">The object type</typeparam>
    /// <returns>Instance of object from type T, after bind to the IConfiguration given</returns>
    public static T BindToObject<T>(this IConfiguration source, BinderOptions binderOptions, ILogger? logger = null)
        where T : new()
    {
        return (T?)BindToObject(typeof(T), source.GetDictionaryFromConfiguration(), binderOptions, logger) ?? new T();
    }

    /// <summary>
    /// Converts <see cref="IConfiguration"/> to an object of the given runtime type.
    /// </summary>
    public static object BindToObject(this IConfiguration source, Type objectType, BinderOptions binderOptions,
        ILogger? logger = null)
    {
        return BindToObject(objectType, source.GetDictionaryFromConfiguration(), binderOptions, logger) ??
               objectType.CreateInstance() ??
               throw new ArgumentException($"Failed to create object from type {objectType.Name}");
    }

    private static object? BindToObject(Type objectType, Dictionary<string, object?> sourceDictionary,
        BinderOptions binderOptions, ILogger? logger = null, string parentPath = "")
    {
        // Create an instance of the object type
        var instance = objectType.CreateInstance();
        if (objectType == typeof(IConfiguration))
            return sourceDictionary.BindToDictionaryIConfiguration();

        if (instance == null)
            throw new ArgumentException($"Failed to create object from type {nameof(objectType)}");

        var bindingFlags = BindingFlags.Public | BindingFlags.Instance |
                           BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase;

        if (binderOptions.BindNonPublicProperties)
            bindingFlags |= BindingFlags.NonPublic;

        if (objectType.IsTypeDictionary() && sourceDictionary.FirstOrDefault().Key != string.Empty)
            return BindDictionaryTypeProperty(sourceDictionary, binderOptions,
                objectType, logger, parentPath);

        foreach (var (key, value) in sourceDictionary)
        {
            var path = $"{parentPath}{ConfigurationConstants.PathSeparator}{key}";
            // Handle no direct value in IConfiguration
            if (string.IsNullOrEmpty(key))
                return ConvertObjectValueToType(value, new BinderOptions
                {
                    ErrorOnUnknownConfiguration = true,
                    BindNonPublicProperties = binderOptions.BindNonPublicProperties
                }, objectType, logger, path);
            var property = objectType.GetProperty(key, bindingFlags);
            if (property == null)
            {
                // If the property is not found, we skip it and log a warning
                if (binderOptions.ErrorOnUnknownConfiguration)
                    logger?.LogWarning("Property {Key} in path {Path} not found in {Type} object",
                        key, TypeUtils.GetParentPathPrefix(parentPath), objectType.Name);
                continue;
            }

            if (!property.CanWrite)
            {
                // If the property is not writable, we skip it and log a warning
                logger?.LogWarning("Property {key} in path {Path} is not writable", key,
                    TypeUtils.GetParentPathPrefix(parentPath));
                continue;
            }

            var result = ConvertObjectValueToType(value, new BinderOptions
            {
                ErrorOnUnknownConfiguration = true,
                BindNonPublicProperties = binderOptions.BindNonPublicProperties
            }, property.PropertyType, logger, path);
            property.SetValue(instance, result);
        }

        return instance;
    }

    /// <summary>
    /// Handles dictionary type property and returns the instance of the dictionary
    /// </summary>
    /// <param name="dictionary">The dictionary to handle</param>
    /// <param name="binderOptions"> The options of the binder that binds the configurations to the
    /// configurations object</param>
    /// <param name="type">The dictionary type</param>
    /// <param name="logger">ILogger for logging></param>
    /// <param name="parentPath">The path to the property</param>
    /// <returns>Instance of the dictionary/KeyValuePair</returns>
    /// <exception cref="ArgumentException">Thrown if instance of the type cannot be created</exception>
    private static object? BindDictionaryTypeProperty(Dictionary<string, object?> dictionary,
        BinderOptions binderOptions, Type type, ILogger? logger = null, string parentPath = "")
    {
        // index 0 - key type
        // index 1 - value type
        var keyType = type.GetGenericArguments()[0];
        var valueType = type.GetGenericArguments()[1];
        var instance = type.CreateInstance();
        if (instance is IDictionary dictionaryInstance)
        {
            foreach (var (key, value) in dictionary)
            {
                var path = $"{parentPath}{ConfigurationConstants.PathSeparator}{key}";
                var convertedKey = TypeUtils.ConvertSimpleValueToType(keyType, key, logger, path);
                if (convertedKey == null)
                    throw new ArgumentException($"Failed to convert key {key} to type {keyType.Name} " +
                                                $"in path {path}");
                var result = ConvertObjectValueToType(value, new BinderOptions
                {
                    ErrorOnUnknownConfiguration = true,
                    BindNonPublicProperties = binderOptions.BindNonPublicProperties
                }, valueType, logger, path);
                dictionaryInstance[convertedKey] = result;
            }
        }
        else if (type.IsTypeKeyValuePair())
        {
            foreach (var (key, value) in dictionary)
            {
                var path = $"{parentPath}{ConfigurationConstants.PathSeparator}{key}";
                var convertedKey = TypeUtils.ConvertSimpleValueToType(keyType, key, logger, path);
                var result = ConvertObjectValueToType(value, new BinderOptions
                {
                    ErrorOnUnknownConfiguration = true,
                    BindNonPublicProperties = binderOptions.BindNonPublicProperties
                }, valueType, logger, path);
                instance = Activator.CreateInstance(type, convertedKey, result);
            }
        }
        else
            throw new ArgumentException($"Failed to create instance of type {type.Name} object in path " +
                                        $"{parentPath}");

        return instance;
    }


    /// <summary>
    /// Adds list items to the list instance and returns it 
    /// </summary>
    /// <param name="listType">The list type</param>
    /// <param name="isArray">Is the list array or regular list</param>
    /// <param name="configurationsList">The list given in the configurations</param>
    /// <param name="binderOptions"> The options of the binder that binds the configurations to the
    /// configurations object</param>
    /// <param name="logger">ILogger for logging></param>
    /// <param name="parentPath">The path to the property</param>
    /// <returns>The list instance</returns>
    private static IList? CreateListFromTypeAndConvertConfigurationListToIt(Type listType, bool isArray,
        IEnumerable configurationsList, BinderOptions binderOptions, ILogger? logger = null, string parentPath = "")
    {
        var (listItemsType, listInstance) = ListUtils.GetListItemsTypeAndInstance(listType, parentPath);
        var listItems = configurationsList.Cast<object>().Where(item => item != null).ToList();
        var itemIndex = 0;
        foreach (var listItem in listItems)
        {
            var path = $"{parentPath}{ConfigurationConstants.PathSeparator}{itemIndex}";
            var obj = ConvertObjectValueToType(listItem, new BinderOptions
            {
                ErrorOnUnknownConfiguration = true,
                BindNonPublicProperties = binderOptions.BindNonPublicProperties
            }, listItemsType, logger, path);

            if (obj == null)
            {
                logger?.LogDebug("Item at index {ItemIndex} in path {Path} is null, extracting list"
                    , itemIndex, path);
                continue;
            }

            itemIndex++;
            listInstance?.Add(obj);
        }

        // if array create array instance
        if (isArray)
        {
            var arrayInstance = Array.CreateInstance(listItemsType, listItems.Count);
            listInstance?.CopyTo(arrayInstance, 0);
            return arrayInstance;
        }

        return listInstance;
    }


    /// <summary>
    /// Converts a value to the given property type and returns it
    /// </summary>
    /// <param name="value">The value to convert</param>
    /// <param name="binderOptions">Binder options</param>
    /// <param name="propertyType">The property type to convert to</param>
    /// <param name="logger">ILogger for logging></param>
    /// <param name="parentPath">The path to the property</param>
    /// <returns>The converted value</returns>
    private static object? ConvertObjectValueToType(object? value, BinderOptions binderOptions, Type propertyType,
        ILogger? logger = null, string parentPath = "")
    {
        var binderOptionsToPass = new BinderOptions
        {
            ErrorOnUnknownConfiguration = true,
            BindNonPublicProperties = binderOptions.BindNonPublicProperties
        };
        switch (value)
        {
            // If the value is null, we set it as null
            case null:
                if (propertyType == typeof(string))
                    return string.Empty;
                return propertyType.GetDefaultValue();
            // If the value is a dictionary, we recursively convert it to a nested object
            case Dictionary<string, object?> nestedDict:
            {
                object? nestedObject;
                if (propertyType == typeof(IConfiguration))
                    nestedObject = nestedDict.BindToDictionaryIConfiguration();
                else if (propertyType.IsTypeList())
                {
                    var nestedList = nestedDict!.ConvertDictionaryToListAccordingToKeys();
                    logger?.LogDebug("Dictionary under the path {ParentPath} converted to list duo to property " +
                                     "type being list", parentPath);
                    nestedObject = CreateListFromTypeAndConvertConfigurationListToIt(propertyType, propertyType.IsArray,
                        nestedList,
                        binderOptionsToPass, logger,
                        parentPath);
                }
                else if (propertyType.IsTypeDictionary() || propertyType.IsTypeKeyValuePair())
                    nestedObject = BindDictionaryTypeProperty(nestedDict, binderOptions, propertyType
                        , logger, parentPath);
                else
                    nestedObject = BindToObject(propertyType, nestedDict, binderOptionsToPass, logger, parentPath);

                return nestedObject;
            }
            // If the value is a list, we recursively convert it to a nested object
            // and add it to the list
            case IList nestedList:
            {
                if (propertyType == typeof(IConfiguration))
                {
                    var result = new Dictionary<string, string?>();
                    nestedList.GetInMemoryCollectionFromList(string.Empty, result);
                    return new ConfigurationBuilder()
                        .AddInMemoryCollection(result).Build();
                }

                IList? resultList;
                if (propertyType.IsTypeDictionary())
                {
                    var convertedListType = typeof(List<>).MakeGenericType(propertyType.GetGenericArguments()[1]);
                    resultList = CreateListFromTypeAndConvertConfigurationListToIt(convertedListType,
                        convertedListType.IsArray, nestedList,
                        binderOptionsToPass, logger,
                        parentPath);
                    return resultList?.ConvertConfigurationListToDictionaryWithIndexesAsKeys(propertyType,
                        binderOptions, logger, parentPath);
                }

                resultList = CreateListFromTypeAndConvertConfigurationListToIt(propertyType, propertyType.IsArray,
                    nestedList,
                    binderOptionsToPass, logger,
                    parentPath);
                return resultList;
            }
            // If the value is not a dictionary or list, we convert it to the property type
            // and set it as the property value
            default:
            {
                object? result;
                // if the property type is IConfiguration and the value is simple, we set the value to
                // {string.empty, value}
                if (propertyType == typeof(IConfiguration))
                {
                    result = TypeUtils.ConvertSimpleValueToType(value.GetType(), value, logger, parentPath);
                    return new ConfigurationBuilder()
                        .AddInMemoryCollection(new List<KeyValuePair<string, string?>>
                        {
                            new(string.Empty, result?.ToString())
                        }).Build();
                }

                result = TypeUtils.ConvertSimpleValueToType(propertyType, value, logger, parentPath);
                return result;
            }
        }
    }

    private static IDictionary ConvertConfigurationListToDictionaryWithIndexesAsKeys(this IList list, Type dictType,
        BinderOptions binderOptions,
        ILogger? logger,
        string parentPath = "")
    {
        var dict = (IDictionary)dictType.CreateInstance()!;
        for (var listIndex = 0; listIndex < list.Count; listIndex++)
            dict[
                    // index 0 - dictionary key type
                    TypeUtils.ConvertSimpleValueToType(dictType.GetGenericArguments()[0], listIndex, logger,
                        parentPath)!] =
                ConvertObjectValueToType(list[listIndex], new BinderOptions
                    {
                        ErrorOnUnknownConfiguration = true,
                        BindNonPublicProperties = binderOptions.BindNonPublicProperties
                    },
                    // index 1 - dictionary value type
                    dictType.GetGenericArguments()[1], logger, parentPath);
        return dict;
    }
}
