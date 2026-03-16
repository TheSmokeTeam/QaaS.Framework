using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

/// <summary>
/// Validates all items in an enumerable represent a valid path
/// </summary>
public class AllPathsInEnumerableValidAttribute: ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // If item is not an enumerable or if its null - validation is automatically successful 
        if (value is not IEnumerable enumerable) return ValidationResult.Success;

        foreach (var item in enumerable)
        {
            if (item == null)
            {
                return new ValidationResult("Path in enumerable cannot be null.");
            }

            var filePath = item.ToString();
            if (string.IsNullOrWhiteSpace(filePath) || filePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return new ValidationResult(
                    $"Path in enumerable {filePath} is not a valid path.");
            }
        }
        return ValidationResult.Success;
    }
}
