using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

/// <summary>
/// Base attribute for conditional validation that checks if any of the given conditions are true.
/// Conditions are provided in a list of key value pairs where each pair in the list is a property name in the object.
/// If that property's value is equal to the value in the pair then the condition is true.
/// Conditions equal capabilities are limited to comparing the .ToString() value of the field.
/// </summary>
public abstract class ConditionalValidationAttribute : ValidationAttribute
{
    protected readonly List<KeyValuePair<string, object?>> _conditions;
    
    /// <summary>
    /// Constructs the conditional validation attribute with a string representing the conditions
    /// </summary>
    /// <param name="stringConditions"> A string representing a list of key value pairs where every item in the
    /// list is separated by `,` and every pair is separated by `:`, ignores spaces.
    /// for example: "age:5,name:REDA,child:true"
    ///  </param>
    protected ConditionalValidationAttribute(string stringConditions)
    {
        _conditions = stringConditions.Replace(" ", "").Split(",").Select(pair =>
        {
            var splitPair = pair.Replace(" ", "").Split(":");
            return new KeyValuePair<string, object?>(splitPair[0], splitPair[1]);
        }).ToList();
    }
    
    /// <summary>
    /// Constructs the conditional validation attribute with a string representing a field name and an
    /// array of strings representing the possible values for that field
    /// </summary>
    /// <param name="fieldName"> Name of the field, will be used as the key in every pair in the key value
    /// pair conditions list </param>
    /// <param name="possibleFieldValues"> Possible values of the field used as the values in the key
    /// value pair conditions list </param>
    protected ConditionalValidationAttribute(string fieldName, params object?[] possibleFieldValues)
    {
        _conditions = possibleFieldValues.Select(possibleValue
            => new KeyValuePair<string, object?>(fieldName, possibleValue)).ToList();
    }
    
    /// <summary>
    /// Constructs the conditional validation attribute with 2 string arrays representing the conditions
    /// </summary>
    /// <param name="stringConditionsFields"> All the keys in the key value pair list, matched with values by index
    /// </param>
    /// <param name="stringConditionsValues"> All the values in the key value pair list, matched with values by index
    /// </param>
    protected ConditionalValidationAttribute(string[] stringConditionsFields, params object[] stringConditionsValues)
    {
        if (stringConditionsFields.Length != stringConditionsValues.Length)
            throw new NotSupportedException(
                "Number of fields and values in condition is not the same, must have a" +
                " field for every value and a value for every field");
        _conditions = new List<KeyValuePair<string, object?>>(stringConditionsFields.Length);
        for (var fieldIndex = 0; fieldIndex < stringConditionsFields.Length; fieldIndex++)
        {
            _conditions.Add(new KeyValuePair<string, object?>(
                stringConditionsFields[fieldIndex], stringConditionsValues[fieldIndex]));
        }
    }
    
    /// <summary>
    /// Determines whether the condition is met for a given validation context
    /// </summary>
    protected bool CheckIfAnyConditionIsMet(ValidationContext validationContext)
    {
        var objectProperties = validationContext.ObjectType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        var atLeastOnePropertyConditionIsMet = false;
        
        foreach (var (propertyName, conditionalPropertyValue) in _conditions)
        {
            var propertyInfo = 
                objectProperties.FirstOrDefault(property => property.Name == propertyName);
            if (propertyInfo == null)
                throw new NotSupportedException($"{propertyName} Property in" +
                                                $" {validationContext.ObjectType.Name} not found when trying" +
                                                $" to validate with {GetType().Name}");
                
            var actualPropertyValue = propertyInfo.GetValue(validationContext.ObjectInstance);
            // Specific equals for case where property is null
            if (actualPropertyValue is null)
            {
                if (conditionalPropertyValue == null)
                    atLeastOnePropertyConditionIsMet = true;
                continue;
            }
                
            // If property is equal to the given value
            if (actualPropertyValue.Equals(conditionalPropertyValue))
                atLeastOnePropertyConditionIsMet = true;
        }
        
        return atLeastOnePropertyConditionIsMet;
    }
}