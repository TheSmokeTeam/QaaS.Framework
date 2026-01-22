using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

/// <summary>
/// Validates that all of the items in an enumerable property within an enumerable can be found in the parent enumerable
/// as a certain property
/// </summary>
public class AllItemsInEnumerablePropertyInEnumerableExistAsPropertyInEnumerableAttribute: ValidationAttribute
{
    private readonly string _enumerablePropertyName;
    private readonly string _propertyName;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="enumerablePropertyName"> The name of the enumerable property within every item in the enumerable
    /// to validate that contains existing property values </param>
    /// <param name="propertyName"> The name of the property within the enumerable that the enumerable property
    /// should contain existing values of in order for validation to pass </param>
    public AllItemsInEnumerablePropertyInEnumerableExistAsPropertyInEnumerableAttribute(string enumerablePropertyName,
        string propertyName)
    {
        _enumerablePropertyName = enumerablePropertyName;
        _propertyName = propertyName;
    }
    
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // If item is not an enumerable or if its null - validation is automatically successful 
        if (value is not IEnumerable enumerable) return ValidationResult.Success;
        
        var propertyValues = (from object? item in enumerable select (item.GetType().GetProperty(_propertyName) 
                                         ?? throw new ArgumentException($"Could not find {_propertyName} property")).GetValue(item) 
                                         ?? throw new ArgumentException(
                                             $"Could not find {_propertyName} property in one of the enumerable items")).ToList();
        foreach (var item in enumerable)
        {
            var enumerableProperty = item.GetType().GetProperty(_enumerablePropertyName,
                                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static) 
                                     ?? throw new ArgumentException($"Could not find {_enumerablePropertyName} property");
            
            if (enumerableProperty.GetValue(item) is not IEnumerable enumerablePropertyValue)
                throw new ArgumentException($"{_enumerablePropertyName} property is not an enumerable");
            
            if (enumerablePropertyValue.Cast<object?>().Any(enumerablePropertyItem =>
                    !propertyValues.Contains(enumerablePropertyItem)))
                return new ValidationResult($"{_enumerablePropertyName} contains an item not" +
                                            $" found in the property {_propertyName} of any of the items in the enumerable");
        }
        return ValidationResult.Success;
    }
}