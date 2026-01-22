using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

/// <summary>
/// Attribute that makes property null if any of the given conditions are true
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class NullIfAnyAttribute : ConditionalValidationAttribute
{
    public NullIfAnyAttribute(string stringConditions) : base(stringConditions)
    {
    }

    public NullIfAnyAttribute(string fieldName, params object?[] possibleFieldValues) : base(fieldName, possibleFieldValues)
    {
    }

    public NullIfAnyAttribute(string[] stringConditionsFields, params object[] stringConditionsValues) : base(stringConditionsFields, stringConditionsValues)
    {
    }

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // If the field is null, it's always valid
        if (value is null) return ValidationResult.Success;

        // Check if any condition is met
        if (CheckIfAnyConditionIsMet(validationContext))
        {
            return new ValidationResult($"At least one of the conditions: " +
                                        $"[{string.Join(", ", _conditions.Select(pair => $"{pair.Key}: {pair.Value}"))}] in" +
                                        $" `{validationContext.ObjectType.Name}` are met, " +
                                        $"making the field `{validationContext.DisplayName}` required to be null, yet it has a value");
        }

        return ValidationResult.Success;
    }
}