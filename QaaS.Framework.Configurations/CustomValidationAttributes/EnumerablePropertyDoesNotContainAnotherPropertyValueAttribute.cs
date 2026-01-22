using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

/// <summary>
/// Validates the enumerable property the attribute is put on does not contain another given property's value
/// </summary>
public class EnumerablePropertyDoesNotContainAnotherPropertyValueAttribute: ValidationAttribute
{
    private readonly string _propertyName;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="propertyName"> The name of the property that the enumerable beneath this attribute
    /// should not contain values of in order for validation to pass </param>
    public EnumerablePropertyDoesNotContainAnotherPropertyValueAttribute(
        string propertyName)
    {
        _propertyName = propertyName;
    }
    
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // If item is not an enumerable or if its null - validation is automatically successful 
        if (value is not IEnumerable enumerable) return ValidationResult.Success;

        var objectPropertyValue = (validationContext.ObjectType.GetProperty(_propertyName) ??
                               throw new ArgumentException($"Could not find {_propertyName} property"))
            .GetValue(validationContext.ObjectInstance);
        
        if (enumerable.Cast<object?>().Contains(objectPropertyValue))
        {
            return new ValidationResult($"{validationContext.MemberName} contains an item with the same value as" +
                                        $" the value under the property {_propertyName}");
        }
        return ValidationResult.Success;
    }
}