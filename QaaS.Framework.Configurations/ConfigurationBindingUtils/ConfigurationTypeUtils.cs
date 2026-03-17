using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace QaaS.Framework.Configurations.ConfigurationBindingUtils;

/// <summary>
/// Utility class for type operations
/// </summary>
internal static class TypeUtils
{
    /// <summary>
    /// Create an instance of the type
    /// </summary>
    /// <param name="type">The type to create instance of</param>
    /// <returns>Instance from the type</returns>
    internal static object? CreateInstance(this Type type)
    {
        if (!type.IsInterface) 
            return Activator.CreateInstance(type);
        return type == typeof(IConfiguration) ? new ConfigurationBuilder().
            AddInMemoryCollection(DictionaryUtils.CreateConfigurationDictionary<string?>()).Build() : null;
    }
    
    /// <summary>
    /// Convert the simple value to the type
    /// <remarks>Supports primitive types and enums</remarks>
    /// </summary>
    /// <param name="type">The type to convert to</param>
    /// <param name="value">The value to convert</param>
    /// <param name="logger">ILogger for logging></param>
    /// <param name="parentPath">The path to the property</param>
    /// <returns>The converted object</returns>
    internal static object? ConvertSimpleValueToType(Type type, object value,
        ILogger? logger = null, string parentPath = "")
    {
        object? result;
        if((type.IsEnum || (Nullable.GetUnderlyingType(type)?.IsEnum ?? false)) 
           && value is string stringValue)
        {
            // If the property type is an enum, we parse the value to the enum type
            // and set the value to the enum
            var enumType = Nullable.GetUnderlyingType(type) ?? type;
            try
            {
                result = Enum.Parse(enumType, stringValue);
            }
            catch (Exception exception)
            {
                LogOnUnmatchedType(value, type, logger, parentPath, exception);
                result = default;
            }
        }
        else
        {
            try
            {
                // get underlying type if nullable 
                var targetType = Nullable.GetUnderlyingType(type) ?? type;
                result = Convert.ChangeType(value, targetType);
            }
            catch (Exception exception)
            {
                // null if convert failed(unmatched type)
                LogOnUnmatchedType(value, type, logger, parentPath, exception);
                result = default;
            }
        }

        return result;
    }

    private static void LogOnUnmatchedType(object? value, Type propertyType, ILogger? logger, string parentPath,
        Exception exception)
    {
        logger?.LogWarning("Failed to bind configurations because value" +
                           " {Value} at path {Path} could not be converted to type {Type}," +
                           " Exception encountered is {Exception}", 
            value, GetParentPathPrefix(parentPath), propertyType, exception.Message);
    }
    
    /// <summary>
    /// Get the parent path prefix for logging purposes
    /// </summary>
    internal static string GetParentPathPrefix(string parentPath) =>
        parentPath.TrimStart(ConfigurationConstants.PathSeparator.ToCharArray()) == string.Empty
            ? string.Empty
            : $"{parentPath.TrimStart(ConfigurationConstants.PathSeparator.ToCharArray())} -";

    /// <summary>
    /// Get the default value for the type
    /// </summary>
    internal static object? GetDefaultValue(this Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
}
