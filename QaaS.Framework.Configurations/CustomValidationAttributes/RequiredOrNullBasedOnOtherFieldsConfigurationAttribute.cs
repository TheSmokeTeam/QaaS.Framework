using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

/// <summary>
/// A validation attribute that requires a property to be configured based on the nullability state of other properties.
/// </summary>
/// <remarks>
/// This attribute allows conditional validation where the configuration state of one property
/// determines whether another property must be configured or must be null/empty.
/// </remarks>
public class RequiredOrNullBasedOnOtherFieldsConfiguration : ValidationAttribute
{
    private readonly string[] _propertyNames;
    private readonly bool[] _expectedValues;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequireConfigureAccordingToNullabilityAttribute"/> class.
    /// </summary>
    /// <param name="propertyNames">An array of property names to check.</param>
    /// <param name="expectedValues">An array of boolean values indicating the expected configuration state for each corresponding property.
    /// True means the property should be configured (not null/empty), false means it should not be configured.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="propertyNames"/> or <paramref name="expectedValues"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the number of property names does not match the number of expected values.
    /// </exception>
    public RequiredOrNullBasedOnOtherFieldsConfiguration(string[] propertyNames, params bool[] expectedValues)
    {
        _propertyNames = propertyNames ?? throw new ArgumentNullException(nameof(propertyNames));
        _expectedValues = expectedValues ?? throw new ArgumentNullException(nameof(expectedValues));

        if (_propertyNames.Length != _expectedValues.Length)
        {
            throw new ArgumentException("The number of property names must match the number of expected values.");
        }
    }

    /// <summary>
    /// Determines whether the specified value is valid according to the configured rules.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The context information about the validation operation.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether the value is valid.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when a specified property name is not found in the target object type.
    /// </exception>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        var instance = validationContext.ObjectInstance;
        var objectType = instance.GetType();
        var validatingPropertyName = validationContext.MemberName;
        
        for (var i = 0; i < _propertyNames.Length; i++)
        {
            var propertyName = _propertyNames[i];
            var expectedValue = _expectedValues[i];

            var propertyInfo = objectType.GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (propertyInfo == null)
                throw new ArgumentException($"Property '{propertyName}' not found in type '{objectType.Name}'.");
            
            var propertyValue = propertyInfo.GetValue(instance);
            
            var isConfigured = IsConfigured(propertyValue);
            switch (isConfigured)
            {
                case true when expectedValue && !IsConfigured(value):
                    return new ValidationResult(
                        $"The {validatingPropertyName} field is required when {propertyName} is configured.",
                        [validatingPropertyName!]);
                
                case true when !expectedValue && IsConfigured(value):
                    return new ValidationResult(
                        $"The {validatingPropertyName} field must be empty when {propertyName} is configured.",
                        [validatingPropertyName!]);
            }
        }
        
        return ValidationResult.Success;
    }
    
    /// <summary>
    /// Determines whether the specified value is considered "configured" (not null).
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value is considered configured; otherwise, false.</returns>
    private static bool IsConfigured(object? value)
    {
        return value != null;
    }
}
