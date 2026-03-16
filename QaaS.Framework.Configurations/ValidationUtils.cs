using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

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
        if (obj == null) return true;
        
        var localResults = new List<ValidationResult>();
        var isValid = obj.TryValidateObject(ref localResults, bindingFlags);

        // Adds validation results to result with the full path to the validation 
        results.AddRange(localResults.Select(result =>
        {
            var trimmedParentPath = parentPath.TrimStart(ConfigurationConstants.PathSeparator.ToCharArray());
            var parentPathPrefixToErrorMessage =
                trimmedParentPath == "" ? trimmedParentPath : $"{trimmedParentPath} - ";
            result.ErrorMessage = $"{parentPathPrefixToErrorMessage}{result.ErrorMessage}";
            return result;
        }));
            
        // Get all properties of sub object to validate them, If an object has a recursive reference to itself
        // the function will stackoverflow, so properties with the same type as `obj` are ignored
        var properties = obj.GetType().GetProperties(bindingFlags).Where(p => p.GetIndexParameters().Length == 0 &&
                                                                              p.PropertyType != obj.GetType());
        foreach (var property in properties)
        {
            var getter = property.GetGetMethod(nonPublic: true);
            if (getter == null)
                continue;

            var value = getter.Invoke(obj, null);
            var propertyPath = $"{parentPath}{ConfigurationConstants.PathSeparator}{property.Name}";
                
            // Handle enumerable properties
            if (value is IEnumerable enumerableValue && !(value is string))
            {
                // Handle Dictionary properties
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
                // Handle none dictionary enumerable properties
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

            // Handle non enumerable properties
            else if (value != null && !TryValidateObjectRecursive(value, results, propertyPath, bindingFlags))
                isValid = false;
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

        // Handle primitive types and enums validation
        if (objType.IsPrimitive || objType == typeof(string) || objType.IsEnum || objType == typeof(DateTime))
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

            foreach (var property in objType.GetProperties(bindingFlags)
                         .Where(property => property.GetMethod?.IsPublic != true))
            {
                // Ignore indexed properties
                if (property.GetIndexParameters().Length > 0) 
                    continue;
                
                var getter = property.GetGetMethod(nonPublic: true);
                if (getter == null)
                    continue;
                
                var propertyValue = getter.Invoke(obj, null);
                
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
}
