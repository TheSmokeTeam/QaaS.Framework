using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace QaaS.Framework.Configurations;

/// <summary>
/// All utility functions related to validating objects
/// </summary>
public static class ValidationUtils
{
    /// <summary>
    /// Recursively validates the given object and its nested objects, including objects within lists and
    /// objects within dictionaries values.
    /// also keeps all the validation results in the given results list.
    /// This function relies on the DataAnnotations validation attributes applied to the object properties.
    /// </summary>
    /// <param name="obj"> The object to validate </param>
    /// <param name="results"> A list of results that all the validation results will be added to </param>
    /// <param name="parentPath"> The path to the parent object of the object currently being validated </param>
    /// <param name="bindingFlags">The binding flags to use for validation</param>
    /// <returns> true if the object and all of its sub objects are valid and false if not </returns>
    public static bool TryValidateObjectRecursive(object? obj, List<ValidationResult> results, string parentPath = "",
        BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance)
    {
        if (obj == null)
            return true;

        if (IsTerminalType(obj.GetType()))
        {
            var terminalResults = new List<ValidationResult>();
            var terminalValid = obj.TryValidateObject(ref terminalResults, bindingFlags);
            results.AddRange(PrefixValidationResults(terminalResults, parentPath));
            return terminalValid;
        }

        var localResults = new List<ValidationResult>();
        var isValid = obj.TryValidateObject(ref localResults, bindingFlags);
        results.AddRange(PrefixValidationResults(localResults, parentPath));

        var properties = GetValidationProperties(obj.GetType(), bindingFlags)
            .Where(property => property.GetIndexParameters().Length == 0 &&
                               property.PropertyType != obj.GetType() &&
                               ShouldTraverseProperty(property, bindingFlags));
        foreach (var property in properties)
        {
            if (!TryGetPropertyValue(obj, property, out var value))
                continue;

            if (value == null || IsTerminalType(value.GetType()))
                continue;

            var propertyPath = $"{parentPath}{ConfigurationConstants.PathSeparator}{property.Name}";

            if (value is IEnumerable enumerableValue && value is not string)
            {
                if (value is IDictionary dictionary)
                {
                    foreach (var key in dictionary.Keys)
                    {
                        var entryPath = $"{propertyPath}{ConfigurationConstants.PathSeparator}{key}";
                        var entry = dictionary[key];
                        if (entry != null && !TryValidateObjectRecursive(entry, results, entryPath, bindingFlags))
                            isValid = false;
                    }
                }
                else
                {
                    var itemIndex = 0;
                    foreach (var item in enumerableValue)
                    {
                        var itemPath = $"{propertyPath}{ConfigurationConstants.PathSeparator}{itemIndex}";
                        if (item != null && !TryValidateObjectRecursive(item, results, itemPath, bindingFlags))
                            isValid = false;

                        itemIndex++;
                    }
                }
            }
            else if (!TryValidateObjectRecursive(value, results, propertyPath, bindingFlags))
            {
                isValid = false;
            }
        }

        return isValid;
    }

    /// <summary>
    /// Validate the object or his properties using the DataAnnotations validation attributes
    /// </summary>
    /// <returns>True if the object is valid</returns>
    private static bool TryValidateObject(this object obj, ref List<ValidationResult> results,
        BindingFlags bindingFlags)
    {
        var validationResults = new List<ValidationResult>();
        var objType = obj.GetType();

        if (IsTerminalType(objType))
        {
            var validationContext = new ValidationContext(obj, null, null)
            {
                MemberName = string.Empty
            };

            var validationAttributes = objType.GetCustomAttributes<ValidationAttribute>();
            foreach (var validationAttribute in validationAttributes)
            {
                var result = validationAttribute.GetValidationResult(obj, validationContext);
                if (result != ValidationResult.Success)
                    validationResults.Add(result!);
            }
        }
        else
        {
            _ = Validator.TryValidateObject(obj, new ValidationContext(obj), validationResults, true);
            var objectValidationContext = new ValidationContext(obj, null, null)
            {
                MemberName = string.Empty
            };

            foreach (var validationAttribute in objType.GetCustomAttributes<ValidationAttribute>())
            {
                var result = validationAttribute.GetValidationResult(obj, objectValidationContext);
                if (result != ValidationResult.Success && result != null)
                    validationResults.Add(result);
            }

            if ((bindingFlags & BindingFlags.NonPublic) == 0)
            {
                results.AddRange(DistinctValidationResults(validationResults));
                return !validationResults.Any();
            }

            foreach (var property in GetValidationProperties(objType, bindingFlags)
                         .Where(property => property.GetMethod?.IsPublic != true))
            {
                if (property.GetIndexParameters().Length > 0)
                    continue;

                if (!TryGetPropertyValue(obj, property, out var propertyValue))
                    continue;

                var validationContext = new ValidationContext(obj, null, null)
                {
                    MemberName = property.Name
                };

                var validationAttributes = property.GetCustomAttributes<ValidationAttribute>();
                foreach (var validationAttribute in validationAttributes)
                {
                    var result = validationAttribute.GetValidationResult(propertyValue, validationContext);
                    if (result != ValidationResult.Success && result != null)
                        validationResults.Add(result);
                }
            }
        }

        results.AddRange(DistinctValidationResults(validationResults));
        return !validationResults.Any();
    }

    private static IEnumerable<ValidationResult> PrefixValidationResults(IEnumerable<ValidationResult> validationResults,
        string parentPath)
    {
        var trimmedParentPath = parentPath.TrimStart(ConfigurationConstants.PathSeparator.ToCharArray());
        var parentPrefix = trimmedParentPath.Length == 0 ? string.Empty : $"{trimmedParentPath} - ";

        return validationResults.Select(result =>
        {
            result.ErrorMessage = $"{parentPrefix}{result.ErrorMessage}";
            return result;
        });
    }

    private static IEnumerable<PropertyInfo> GetValidationProperties(Type type, BindingFlags bindingFlags)
    {
        var includePublic = (bindingFlags & BindingFlags.Public) != 0;
        var includeNonPublic = (bindingFlags & BindingFlags.NonPublic) != 0;
        if ((bindingFlags & BindingFlags.Instance) == 0 || (!includePublic && !includeNonPublic))
            yield break;

        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        for (var currentType = type; currentType != null && currentType != typeof(object);
             currentType = currentType.BaseType)
        {
            var currentBindingFlags = BindingFlags.Instance | BindingFlags.DeclaredOnly;
            if (includePublic)
                currentBindingFlags |= BindingFlags.Public;
            if (includeNonPublic)
                currentBindingFlags |= BindingFlags.NonPublic;

            foreach (var property in currentType.GetProperties(currentBindingFlags))
            {
                if (seenNames.Add(property.Name))
                    yield return property;
            }
        }
    }

    private static bool ShouldInspectProperty(PropertyInfo property)
    {
        return property.GetCustomAttributes<ValidationAttribute>().Any()
               || property.GetCustomAttributes<DescriptionAttribute>().Any()
               || property.GetCustomAttributes<DefaultValueAttribute>().Any();
    }

    private static bool ShouldTraverseProperty(PropertyInfo property, BindingFlags bindingFlags)
    {
        if (property.GetMethod?.IsPublic == true)
            return true;

        if ((bindingFlags & BindingFlags.NonPublic) == 0)
            return false;

        return property.GetGetMethod(nonPublic: true) != null &&
               (ShouldInspectProperty(property) || !IsTerminalType(property.PropertyType));
    }

    private static IEnumerable<ValidationResult> DistinctValidationResults(IEnumerable<ValidationResult> validationResults)
    {
        return validationResults
            .GroupBy(result => new
            {
                Message = result.ErrorMessage ?? string.Empty,
                Members = string.Join("|", result.MemberNames.OrderBy(member => member, StringComparer.Ordinal))
            })
            .Select(group => group.First());
    }

    private static bool TryGetPropertyValue(object instance, PropertyInfo property, out object? value)
    {
        try
        {
            value = property.GetValue(instance);
            return true;
        }
        catch (TargetInvocationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is MethodAccessException or ArgumentException)
        {
            var getter = property.GetGetMethod(nonPublic: true);
            if (getter == null)
            {
                value = null;
                return false;
            }

            value = getter.Invoke(instance, null);
            return true;
        }
    }

    private static bool IsTerminalType(Type type)
    {
        var effectiveType = Nullable.GetUnderlyingType(type) ?? type;

        return effectiveType.IsPrimitive
               || effectiveType.IsEnum
               || effectiveType == typeof(string)
               || effectiveType == typeof(decimal)
               || effectiveType == typeof(DateTime)
               || effectiveType == typeof(DateTimeOffset)
               || effectiveType == typeof(TimeSpan)
               || effectiveType == typeof(Guid)
               || effectiveType == typeof(Uri)
               || effectiveType == typeof(Type)
               || typeof(Delegate).IsAssignableFrom(effectiveType)
               || typeof(MemberInfo).IsAssignableFrom(effectiveType)
               || typeof(Assembly).IsAssignableFrom(effectiveType)
               || typeof(IConfiguration).IsAssignableFrom(effectiveType)
               || effectiveType == typeof(IntPtr)
               || effectiveType == typeof(UIntPtr)
               || effectiveType.IsPointer
               || effectiveType.IsByRef;
    }
}
