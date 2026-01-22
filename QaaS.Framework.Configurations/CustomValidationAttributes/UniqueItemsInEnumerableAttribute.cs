using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

/// <summary>
///  Validates that all items in an enumerable are unique.
/// </summary>
public class UniqueItemsInEnumerableAttribute: ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // If item is not an enumerable or if its null - validation is automatically successful 
        if (value is not IEnumerable enumerable) return ValidationResult.Success;
            
        var uniqueItems = new HashSet<object>();
        foreach (var item in enumerable)
        {
            if (uniqueItems.Contains(item))
            {
                return new ValidationResult(
                    $"Duplicate value found for item `{item}` in enumerable");
            }
            uniqueItems.Add(item);
        }
        return ValidationResult.Success;
    }
    
        
}