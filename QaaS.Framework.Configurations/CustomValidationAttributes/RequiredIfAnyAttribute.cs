using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

/// <summary>
/// Attribute that makes property required if any of the given conditions are true
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class RequiredIfAnyAttribute : ConditionalValidationAttribute
{
    public RequiredIfAnyAttribute(string stringConditions) : base(stringConditions)
    {
    }
    
    public RequiredIfAnyAttribute(string fieldName, params object?[] possibleFieldValues) : base(fieldName, possibleFieldValues)
    {
    }
    
    public RequiredIfAnyAttribute(string[] stringConditionsFields, params object[] stringConditionsValues) : base(stringConditionsFields, stringConditionsValues)
    {
    }
    
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // If not null conditions are irrelevant and its valid
        if (value is not null) return ValidationResult.Success;
        
        // Check if any condition is met
        if (CheckIfAnyConditionIsMet(validationContext))
        {
            return new ValidationResult($"At least one of the conditions: " +
                                        $"[{string.Join(", ", _conditions.Select(pair => $"{pair.Key}: {pair.Value}"))}] in" +
                                        $" `{validationContext.ObjectType.Name}` are met, " +
                                        $"making the field `{validationContext.DisplayName}` required, yet it has no value");
        }
        
        return ValidationResult.Success;
    }
}