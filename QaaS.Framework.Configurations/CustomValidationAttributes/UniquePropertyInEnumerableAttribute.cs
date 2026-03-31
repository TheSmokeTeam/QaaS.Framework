using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

/// <summary>
/// Validates that an enumerable of items does not contain any duplicates of values of a certain property.
/// Works only on public instance property fields
/// </summary>
public class UniquePropertyInEnumerableAttribute : ValidationAttribute
{
    private readonly string _fieldName;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="fieldName"> The name of the field that should have unique values in the enumerable </param>
    public UniquePropertyInEnumerableAttribute(string fieldName)
    {
        _fieldName = fieldName;
    }

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // If item is not an enumerable or if its null - validation is automatically successful 
        if (value is not IEnumerable enumerable) return ValidationResult.Success;
            
        var uniqueValues = new HashSet<object?>();
        var itemIndex = 0;
        foreach (var item in enumerable)
        {
            if (item == null)
            {
                return new ValidationResult(
                    $"Null item found at index {itemIndex} while validating unique field `{_fieldName}`.");
            }

            var propertyInfo = item.GetType().GetProperty(_fieldName,BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (propertyInfo == null)
                throw new NotSupportedException(
                    $"Couldn't find public property {_fieldName} in {item.GetType().Name}");
            var fieldValue = propertyInfo.GetValue(item);
            if (uniqueValues.Contains(fieldValue))
            {
                return new ValidationResult(
                    $"Duplicate value found for field `{_fieldName}` in {item.GetType().Name} enumerable");
            }
            uniqueValues.Add(fieldValue);
            itemIndex++;
        }
        return ValidationResult.Success;
    }

}
