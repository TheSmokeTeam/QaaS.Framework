using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

/// <summary>
/// Validates that the property is null unless all given conditions are met by the other properties in the object
/// Basically the property has to be null unless all conditions are true.
/// Conditions are provided in a list of key value pairs where each pair in the list is a property name in the object
/// and if that property's value is equal to the value in the pair then the condition is true.
/// Conditions equal capabilities are limited to comparing the .ToString() value of the field.
/// </summary>
public class NullUnlessAllAttribute : ValidationAttribute
{
    private readonly List<KeyValuePair<string, object?>> _conditions;
        
    /// <summary>
    /// Construct the attribute with a string representing the conditions
    /// </summary>
    /// <param name="stringConditions"> A string representing a list of key value pairs where every item in the
    /// list is seperated by `,` and every pair is seperated by `:`, ignores spaces.
    /// for example: "age:5,name:John,child:true"
    ///  </param>
    public NullUnlessAllAttribute(string stringConditions)
    {
        _conditions = stringConditions.Replace(" ", "").Split(",").Select(pair =>
        {
            var splitPair = pair.Replace(" ", "").Split(":");
            return new KeyValuePair<string, object?>(splitPair[0], splitPair[1]);
        }).ToList();
    }
        
    /// <summary>
    /// Constructs the attribute with 2 string arrays representing the conditions
    /// </summary>
    /// <param name="stringConditionsFields"> All the keys in the key value pair list, matched with values by index
    /// </param>
    /// <param name="objectConditionsValues"> All the values in the key value pair list, matched with values by index
    /// </param>
    public NullUnlessAllAttribute(string[] stringConditionsFields, params object?[] objectConditionsValues)
    {
        if (stringConditionsFields.Length != objectConditionsValues.Length)
            throw new NotSupportedException(
                "Number of fields and values in condition is not the same, must have a" +
                " field for every value and a value for every field");
        _conditions = new List<KeyValuePair<string, object?>>(stringConditionsFields.Length);
        for (var fieldIndex = 0; fieldIndex < stringConditionsFields.Length; fieldIndex++)
        {
            _conditions.Add(new KeyValuePair<string, object?>(
                stringConditionsFields[fieldIndex], objectConditionsValues[fieldIndex]));
        }
    }

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // If null conditions are irrelevant and the value is valid
        if (value is null) return ValidationResult.Success;
            
        var objectProperties = validationContext.ObjectType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        var allPropertiesMeetConditions = true;
        foreach (var (propertyName, conditionalPropertyValue) in _conditions)
        {
            var propertyInfo = 
                objectProperties.FirstOrDefault(property => property.Name == propertyName);
            if (propertyInfo == null)
                throw new NotSupportedException($"{propertyName} Property in" +
                                                $" {validationContext.ObjectType.Name} not found when trying" +
                                                $" to validate with {this.GetType().Name}");
                
            var actualPropertyValue = propertyInfo.GetValue(validationContext.ObjectInstance);
                
            // Specific equals for case where property is null
            if (actualPropertyValue == null)
            {
                if (conditionalPropertyValue != null)
                    allPropertiesMeetConditions = false;
                continue;
            }
                
            // If property is not equal to the given value
            if (!(actualPropertyValue.Equals(conditionalPropertyValue)))
                allPropertiesMeetConditions = false;
        }
        return !allPropertiesMeetConditions ?
            new ValidationResult($"Not All Of The conditions: " +
                                 $"[{string.Join(", ", _conditions.Select(pair => $"{pair.Key}: {pair.Value}"))}] in" +
                                 $" `{validationContext.ObjectType.Name}` are met, " +
                                 $"making the field `{validationContext.DisplayName}` has to be null, yet it has value") 
            : ValidationResult.Success;
    }
}