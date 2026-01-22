using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

/// <summary>
/// Validates at least one of the properties in the given is not null, if no properties are given
/// checks all properties in the object
/// checks the properties of the object the attribute is put over
/// </summary>
public class AtLeastOnePropertyNotNullAttribute: ValidationAttribute
{
    private readonly IEnumerable<string>? _propertyNamesToCheck;
        
    /// <summary>
    /// Initiate attribute with a few specific properties to check and not the whole object
    /// </summary>
    /// <param name="propertyNamesToCheckArray"> A string array representing a list of properties.
    /// At least one of those properties must not be null in order for
    /// this attribute to return valid, if no properties are in the string by default checks all properties in object
    /// </param>
    public AtLeastOnePropertyNotNullAttribute(params string[] propertyNamesToCheckArray)
    {
        _propertyNamesToCheck = propertyNamesToCheckArray;
    }

    /// <summary>
    /// Initiates attribute that checks all properties in the object
    /// </summary>
    public AtLeastOnePropertyNotNullAttribute()
    {
        _propertyNamesToCheck = null;
    }
        
    /// <inheritdoc />
    protected override ValidationResult? IsValid([NotNull] object value, ValidationContext validationContext)
    {
        var valueType = value.GetType();
        var allObjectProperties = valueType.GetProperties(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            
        // If there are no specific properties to check, check them all
        if (_propertyNamesToCheck == null)
        {
            return allObjectProperties.Any(property => property.GetValue(value) != null) ?
                ValidationResult.Success : new ValidationResult(
                    $"All properties in {valueType.Name} are null," +
                    $" at least 1 of them must contain a value");
        }
            
        var specificPropertiesToCheck = allObjectProperties.Where(
            property => _propertyNamesToCheck.Contains(property.Name)).ToArray();

        return specificPropertiesToCheck.Any(property => property.GetValue(value) != null) ?
            ValidationResult.Success : new ValidationResult(
                "All of the following properties: " +
                $"[{string.Join(", ", specificPropertiesToCheck.Select(property => property.Name))}] " +
                $"in {valueType.Name} are null, at least 1 of them must contain a value");
    }
}