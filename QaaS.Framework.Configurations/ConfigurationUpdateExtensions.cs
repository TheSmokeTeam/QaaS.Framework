using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations.ConfigurationBindingUtils;

namespace QaaS.Framework.Configurations;

/// <summary>
/// Public update helpers that preserve existing configuration values when a partial patch omits them.
/// </summary>
public static class ConfigurationUpdateExtensions
{
    private static readonly BinderOptions UpdateBinderOptions = new()
    {
        ErrorOnUnknownConfiguration = true,
        BindNonPublicProperties = true
    };

    /// <summary>
    /// Merges a typed configuration patch into the current configuration.
    /// When both configurations share the same runtime type, omitted fields are preserved from the current value.
    /// When the runtime type changes, the incoming configuration replaces the current one.
    /// </summary>
    public static TConfiguration UpdateConfiguration<TConfiguration>(
        this TConfiguration? currentConfiguration,
        TConfiguration incomingConfiguration)
        where TConfiguration : class
    {
        ArgumentNullException.ThrowIfNull(incomingConfiguration);

        if (currentConfiguration == null)
            return incomingConfiguration;

        if (currentConfiguration.GetType() != incomingConfiguration.GetType())
            return incomingConfiguration;

        try
        {
            return currentConfiguration.MergeConfiguration(incomingConfiguration)!;
        }
        catch (MissingMethodException)
        {
            MergeIntoCurrent(currentConfiguration, incomingConfiguration);
            return currentConfiguration;
        }
        catch (ArgumentException) when (!HasPublicParameterlessConstructor(incomingConfiguration.GetType()))
        {
            MergeIntoCurrent(currentConfiguration, incomingConfiguration);
            return currentConfiguration;
        }
    }

    /// <summary>
    /// Merges an object-shaped configuration patch into the current typed configuration.
    /// Fields omitted from <paramref name="incomingConfiguration"/> are preserved from the current configuration.
    /// When the current configuration is missing, the incoming object is bound to <typeparamref name="TConfiguration"/>
    /// when possible.
    /// </summary>
    public static TConfiguration UpdateConfiguration<TConfiguration>(
        this TConfiguration? currentConfiguration,
        object incomingConfiguration)
        where TConfiguration : class
    {
        ArgumentNullException.ThrowIfNull(incomingConfiguration);

        if (currentConfiguration is IConfiguration currentIConfiguration)
            return (TConfiguration)(object)currentIConfiguration.UpdateConfiguration(incomingConfiguration);

        if (incomingConfiguration is TConfiguration typedConfiguration)
            return currentConfiguration.UpdateConfiguration(typedConfiguration);

        if (currentConfiguration == null)
        {
            if (typeof(TConfiguration).IsInterface)
            {
                throw new InvalidOperationException(
                    $"Cannot apply an object-shaped patch to {typeof(TConfiguration).Name} without an existing configuration instance.");
            }

            return BindPatchToRuntimeType<TConfiguration>(incomingConfiguration, typeof(TConfiguration));
        }

        try
        {
            var mergedValues = BuildFlatConfigurationValues(currentConfiguration);
            OverlayPatchValues(mergedValues, incomingConfiguration);
            return (TConfiguration)BuildConfigurationRoot(mergedValues)
                .BindToObject(currentConfiguration.GetType(), UpdateBinderOptions);
        }
        catch (ArgumentException) when (!HasPublicParameterlessConstructor(currentConfiguration.GetType()))
        {
            throw new InvalidOperationException(
                $"Cannot apply an object-shaped patch to {currentConfiguration.GetType().Name} because it does not expose a parameterless constructor.");
        }
        catch (MissingMethodException)
        {
            throw new InvalidOperationException(
                $"Cannot apply an object-shaped patch to {currentConfiguration.GetType().Name} because it does not expose a parameterless constructor.");
        }
    }

    /// <summary>
    /// Merges an object-shaped configuration patch into the current <see cref="IConfiguration"/> tree.
    /// Fields omitted from <paramref name="incomingConfiguration"/> are preserved from the current configuration.
    /// </summary>
    public static IConfiguration UpdateConfiguration(
        this IConfiguration? currentConfiguration,
        object incomingConfiguration)
    {
        ArgumentNullException.ThrowIfNull(incomingConfiguration);

        var mergedValues = currentConfiguration == null
            ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : BuildFlatConfigurationValues(currentConfiguration);
        OverlayPatchValues(mergedValues, incomingConfiguration);
        return BuildConfigurationRoot(mergedValues);
    }

    private static TConfiguration BindPatchToRuntimeType<TConfiguration>(object incomingConfiguration, Type runtimeType)
        where TConfiguration : class
    {
        var configuration = BuildConfigurationRoot(BuildFlatConfigurationValues(incomingConfiguration));
        return (TConfiguration)configuration.BindToObject(runtimeType, UpdateBinderOptions);
    }

    private static Dictionary<string, string?> BuildFlatConfigurationValues(object configurationObject)
    {
        if (configurationObject is IConfiguration configuration)
        {
            return configuration.AsEnumerable()
                .Where(pair => pair.Value != null)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        return ConfigurationUtils.GetInMemoryCollectionFromObject(
            configurationObject,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    private static void OverlayPatchValues(Dictionary<string, string?> currentValues, object incomingConfiguration)
    {
        foreach (var patchEntry in BuildFlatConfigurationValues(incomingConfiguration))
        {
            if (patchEntry.Value == null)
                continue;

            currentValues[patchEntry.Key] = patchEntry.Value;
        }
    }

    private static IConfigurationRoot BuildConfigurationRoot(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static void MergeIntoCurrent(object currentConfiguration, object incomingConfiguration)
    {
        var configurationType = incomingConfiguration.GetType();
        var defaultConfiguration = CreateDefaultConfiguration(configurationType);

        foreach (var property in GetMergeableProperties(configurationType))
        {
            var incomingValue = property.GetValue(incomingConfiguration);
            if (incomingValue == null)
                continue;

            var defaultValue = defaultConfiguration != null ? property.GetValue(defaultConfiguration) : null;
            if (AreEquivalentValues(incomingValue, defaultValue, property.PropertyType))
                continue;

            var currentValue = property.GetValue(currentConfiguration);
            if (!IsComplexType(property.PropertyType) ||
                currentValue == null ||
                currentValue.GetType() != incomingValue.GetType())
            {
                property.SetValue(currentConfiguration, incomingValue);
                continue;
            }

            MergeIntoCurrent(currentValue, incomingValue);
        }
    }

    private static IEnumerable<PropertyInfo> GetMergeableProperties(Type type)
    {
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        for (var currentType = type; currentType != null && currentType != typeof(object);
             currentType = currentType.BaseType)
        {
            foreach (var property in currentType.GetProperties(BindingFlags.Instance | BindingFlags.Public |
                                                               BindingFlags.NonPublic |
                                                               BindingFlags.DeclaredOnly))
            {
                if (!seenNames.Add(property.Name))
                    continue;

                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                    yield return property;
            }
        }
    }

    private static bool IsComplexType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return !(underlyingType.IsPrimitive ||
                 underlyingType.IsEnum ||
                 underlyingType == typeof(decimal) ||
                 underlyingType == typeof(DateTime) ||
                 underlyingType == typeof(DateTimeOffset) ||
                 underlyingType == typeof(TimeSpan) ||
                 underlyingType == typeof(Guid) ||
                 underlyingType == typeof(Uri) ||
                 typeof(IEnumerable).IsAssignableFrom(underlyingType) ||
                 underlyingType == typeof(string));
    }

    private static object? CreateDefaultConfiguration(Type type)
    {
        try
        {
            return Activator.CreateInstance(type);
        }
        catch
        {
            return null;
        }
    }

    private static bool AreEquivalentValues(object? left, object? right, Type type)
    {
        if (left == null || right == null)
            return left == right;

        if (type == typeof(string))
            return string.Equals((string)left, (string)right, StringComparison.Ordinal);

        if (left is IEnumerable leftEnumerable && right is IEnumerable rightEnumerable && type != typeof(string))
            return AreEquivalentEnumerables(leftEnumerable, rightEnumerable);

        if (!IsComplexType(type))
            return Equals(left, right);

        return GetMergeableProperties(type)
            .All(property => AreEquivalentValues(
                property.GetValue(left),
                property.GetValue(right),
                property.PropertyType));
    }

    private static bool AreEquivalentEnumerables(IEnumerable left, IEnumerable right)
    {
        var leftEnumerator = left.GetEnumerator();
        var rightEnumerator = right.GetEnumerator();
        try
        {
            while (true)
            {
                var leftMoved = leftEnumerator.MoveNext();
                var rightMoved = rightEnumerator.MoveNext();

                if (leftMoved != rightMoved)
                    return false;

                if (!leftMoved)
                    return true;

                if (!Equals(leftEnumerator.Current, rightEnumerator.Current))
                    return false;
            }
        }
        finally
        {
            (leftEnumerator as IDisposable)?.Dispose();
            (rightEnumerator as IDisposable)?.Dispose();
        }
    }

    private static bool HasPublicParameterlessConstructor(Type type)
    {
        return type.IsValueType || type.GetConstructor(Type.EmptyTypes) != null;
    }
}
