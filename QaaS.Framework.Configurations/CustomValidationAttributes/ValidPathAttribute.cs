using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

/// <summary>
/// Validates if the value loaded to the value below is a valid path (ignores null value)
/// </summary>
public class ValidPathAttribute: ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;
            
        var filePath = value.ToString();
        return !(string.IsNullOrWhiteSpace(filePath) || filePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0) ?
            ValidationResult.Success : new ValidationResult(
                $"Path {filePath} is not a valid path.");
    }
}