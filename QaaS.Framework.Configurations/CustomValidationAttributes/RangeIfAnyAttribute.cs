using System.Reflection;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

using System;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// Validates that a property's value falls within a specific range based on the value of another property.
/// The range is determined by the value of the specified property, and the attribute only applies the range validation
/// if the value of the specified property matches one of the provided values.
/// </summary>
public class RangeIfAnyAttribute : ValidationAttribute
{
    private readonly string _fieldName;
    private readonly object[] _fieldValues;
    private readonly int[] _minValues;
    private readonly int[] _maxValues;

    /// <summary>
    /// Initializes a new instance of the <see cref="RangeIfAnyAttribute"/> class.
    /// This attribute validates a property by checking if its value falls within a specific range.
    /// The range is determined by the value of another property, specified by the <paramref name="fieldName"/> parameter.
    /// The attribute will only apply the range validation if the value of the specified property matches one of the values in the <paramref name="fieldValues"/> array,
    /// and the corresponding range is provided in the <paramref name="minValues"/> and <paramref name="maxValues"/> arrays.
    /// To use this attribute, apply it to a property and pass in the name of the property whose value determines the range,
    /// as well as the values of the determining property and the corresponding minimum and maximum allowed values.
    /// For example: [RangeIfAny("DeterminingProperty", new object[] { "Value1", "Value2" }, new int[] { 1, 2 }, new int[] { 10, 20 })]
    /// </summary>
    /// <param name="fieldName">The name of the property whose value determines the range for validation.</param>
    /// <param name="fieldValues">An array of values of the property specified by <paramref name="fieldName"/>.</param>
    /// <param name="minValues">An array of minimum allowed values, where each value corresponds to the value at the same index in the <paramref name="fieldValues"/> array.</param>
    /// <param name="maxValues">An array of maximum allowed values, where each value corresponds to the value at the same index in the <paramref name="fieldValues"/> array.</param>
    public RangeIfAnyAttribute(string fieldName, object[] fieldValues, int[] minValues, int[] maxValues)
    {
        this._fieldName = fieldName;
        this._fieldValues = fieldValues;
        this._minValues = minValues;
        this._maxValues = maxValues;

        if (fieldValues.Length != minValues.Length || fieldValues.Length != maxValues.Length)
            throw new ArgumentException("Field values, min values and max values are not the same length.",
                nameof(fieldValues));
    }
    
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null) return new ValidationResult("Value to check range of cannot be null.");
    
        var determiningProperty = validationContext.ObjectType.GetProperty(_fieldName,BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        if (determiningProperty == null)
            return new ValidationResult($"Missing field {_fieldName}.");

        var determiningPropertyValue = determiningProperty.GetValue(validationContext.ObjectInstance, null);
        if (determiningPropertyValue == null)
            return new ValidationResult($"Field {_fieldName} cannot be null.");

        foreach (var (conditionValue, minimumAllowedValue, maximumAllowedValue) in _fieldValues.Zip(_minValues, (fieldValue, minValue) => (fieldValue, minValue))
                     .Zip(_maxValues, (range, maxValue) => (range.fieldValue, range.minValue, maxValue)))
        {
            if (Equals(determiningPropertyValue, conditionValue))
            {
                if (value is not IComparable valueToValidate)
                    return new ValidationResult($"{validationContext.DisplayName} must be a comparable type.");

                if (valueToValidate.CompareTo(minimumAllowedValue) < 0 || valueToValidate.CompareTo(maximumAllowedValue) > 0)
                    return new ValidationResult(
                        $"{validationContext.DisplayName} must be between {minimumAllowedValue} and {maximumAllowedValue} but was {valueToValidate}.");

                break;
            }
        }

        return ValidationResult.Success;
    }
}