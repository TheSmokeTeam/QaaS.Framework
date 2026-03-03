using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

/// <summary>
/// Validates at least one of the enumerable properties given is not empty (contains no items or is null),
/// if no properties are given checks all object enumerables
/// works if put over at least a single property in the object
/// </summary>
public class AtLeastOneEnumerablePropertyNotEmptyAttribute: ValidationAttribute
{
        
    private readonly IEnumerable<string>? _enumerablePropertyNamesToCheck;
        
    /// <summary>
    /// Initiate attribute with a few specific properties to check and not the whole object
    /// </summary>
    /// <param name="enumerablePropertyNamesToCheckArray"> An array of property names,
    /// At least one of those properties
    /// must not be an empty/null enumerable in order for this attribute to return valid.
    /// if no properties are given checks all objects enumerables </param>
    public AtLeastOneEnumerablePropertyNotEmptyAttribute(params string[] enumerablePropertyNamesToCheckArray)
    {
        _enumerablePropertyNamesToCheck = enumerablePropertyNamesToCheckArray;
    }

    /// <summary>
    /// Initiates attribute that checks all properties in the object
    /// </summary>
    public AtLeastOneEnumerablePropertyNotEmptyAttribute()
    {
        _enumerablePropertyNamesToCheck = null;
    }
        
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return new ValidationResult($"{validationContext.ObjectType.Name} cannot be null");
        }

        var valueType = value.GetType();

        var allObjectEnumerableProperties = valueType.GetProperties(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Where(property => 
            typeof(IEnumerable).IsAssignableFrom(property.PropertyType));

        if (_enumerablePropertyNamesToCheck == null)
        {
            return allObjectEnumerableProperties.Any(property =>
                property.GetValue(value) is IEnumerable enumerable &&
                enumerable.Cast<object?>().Any()) ?
                ValidationResult.Success : new ValidationResult(
                    $"All enumerable properties in {validationContext.ObjectType.Name} are empty " +
                    $"(contain 0 items or are null), at least 1 enumerable property must have at least 1 item in it");
        }
            
        var specificEnumerablePropertiesToCheck = allObjectEnumerableProperties.Where(
            property => _enumerablePropertyNamesToCheck.Contains(property.Name)).ToArray();
            
        return specificEnumerablePropertiesToCheck.Any(property =>
            property.GetValue(value) is IEnumerable enumerable &&
            enumerable.Cast<object?>().Any()) ?
            ValidationResult.Success : new ValidationResult(
                $"All of the following enumerable properties : " +
                $"[{string.Join(", ", specificEnumerablePropertiesToCheck.Select(property => property.Name))}] " +
                $"in {validationContext.ObjectType.Name} are empty (contain 0 items or are null), " +
                $"at least 1 enumerable property must have at least 1 item in it");
    }
}
