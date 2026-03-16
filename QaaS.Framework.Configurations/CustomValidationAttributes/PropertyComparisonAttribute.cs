using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class PropertyComparisonAttribute : ValidationAttribute
{
    private readonly string _leftPropertyName;
    private readonly string _rightPropertyName;
    private readonly PropertyComparisonOperator _comparisonOperator;

    public PropertyComparisonAttribute(string leftPropertyName, string rightPropertyName,
        PropertyComparisonOperator comparisonOperator)
    {
        _leftPropertyName = leftPropertyName ?? throw new ArgumentNullException(nameof(leftPropertyName));
        _rightPropertyName = rightPropertyName ?? throw new ArgumentNullException(nameof(rightPropertyName));
        _comparisonOperator = comparisonOperator;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        var instance = value ?? validationContext.ObjectInstance;
        var objectType = instance.GetType();
        var leftProperty = GetProperty(objectType, _leftPropertyName);
        var rightProperty = GetProperty(objectType, _rightPropertyName);

        var leftValue = leftProperty.GetValue(instance);
        var rightValue = rightProperty.GetValue(instance);
        if (leftValue is null || rightValue is null)
        {
            return ValidationResult.Success;
        }

        if (leftValue is not IComparable comparableLeft)
        {
            return new ValidationResult(
                $"The property '{_leftPropertyName}' on '{objectType.Name}' must implement IComparable.");
        }

        if (!TryNormalizeComparisonValue(rightValue, leftValue.GetType(), out var normalizedRight))
        {
            return new ValidationResult(
                $"The property '{_rightPropertyName}' on '{objectType.Name}' cannot be compared to '{_leftPropertyName}'.");
        }

        int comparisonResult;
        try
        {
            comparisonResult = comparableLeft.CompareTo(normalizedRight);
        }
        catch (ArgumentException)
        {
            return new ValidationResult(
                $"The property '{_rightPropertyName}' on '{objectType.Name}' cannot be compared to '{_leftPropertyName}'.");
        }

        return IsComparisonSatisfied(comparisonResult)
            ? ValidationResult.Success
            : new ValidationResult(GetDefaultErrorMessage());
    }

    private static PropertyInfo GetProperty(Type objectType, string propertyName)
    {
        return objectType.GetProperty(propertyName,
                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
               ?? throw new ArgumentException($"Property '{propertyName}' not found in type '{objectType.Name}'.");
    }

    private static bool TryNormalizeComparisonValue(object value, Type targetType, out object? normalizedValue)
    {
        if (targetType.IsInstanceOfType(value))
        {
            normalizedValue = value;
            return true;
        }

        try
        {
            normalizedValue = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            normalizedValue = null;
            return false;
        }
    }

    private bool IsComparisonSatisfied(int comparisonResult)
    {
        return _comparisonOperator switch
        {
            PropertyComparisonOperator.LessThan => comparisonResult < 0,
            PropertyComparisonOperator.LessThanOrEqual => comparisonResult <= 0,
            PropertyComparisonOperator.Equal => comparisonResult == 0,
            PropertyComparisonOperator.GreaterThan => comparisonResult > 0,
            PropertyComparisonOperator.GreaterThanOrEqual => comparisonResult >= 0,
            PropertyComparisonOperator.NotEqual => comparisonResult != 0,
            _ => throw new ArgumentOutOfRangeException(nameof(_comparisonOperator), _comparisonOperator, null)
        };
    }

    private string GetDefaultErrorMessage()
    {
        return string.IsNullOrWhiteSpace(ErrorMessage)
            ? $"The property '{_leftPropertyName}' must be {GetOperatorText()} '{_rightPropertyName}'."
            : ErrorMessage!;
    }

    private string GetOperatorText()
    {
        return _comparisonOperator switch
        {
            PropertyComparisonOperator.LessThan => "less than",
            PropertyComparisonOperator.LessThanOrEqual => "less than or equal to",
            PropertyComparisonOperator.Equal => "equal to",
            PropertyComparisonOperator.GreaterThan => "greater than",
            PropertyComparisonOperator.GreaterThanOrEqual => "greater than or equal to",
            PropertyComparisonOperator.NotEqual => "different from",
            _ => throw new ArgumentOutOfRangeException(nameof(_comparisonOperator), _comparisonOperator, null)
        };
    }
}
