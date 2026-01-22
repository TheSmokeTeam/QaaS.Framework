using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

/// <summary>
/// Validates that a property/all items in an enumerable property
/// containing multiple fields that are enumerables of items do not contain any duplicates
/// of values of a certain property across multiple enumerables specified.
/// Works only on public instance property fields
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public class UniquePropertyInEnumerablePropertiesAttribute : ValidationAttribute
{
    /// <inheritdoc />
    public override object TypeId => this; // Required in order to be able to use multiple instances of this attribute on 1 property
    
    private readonly string _fieldName;
    private readonly string[] _enumerablePropertiesNames;
    private readonly string _validationErrorMeaningMessage;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="fieldName"> The name of the field that should have unique values in the enumerables </param>
    /// <param name="enumerablePropertiesNames"> The names of the enumerable properties of the object this attribute
    /// sits on top of to check for duplications with field name</param>
    /// <param name="validationErrorMeaningMessage"> In case of a validation error adds this message to
    /// explain the meaning of the validation failure to the user </param>
    public UniquePropertyInEnumerablePropertiesAttribute(string fieldName, string validationErrorMeaningMessage, params string[] enumerablePropertiesNames)
    {
        _fieldName = fieldName;
        _enumerablePropertiesNames = enumerablePropertiesNames;
        _validationErrorMeaningMessage = validationErrorMeaningMessage;
    }

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not IEnumerable enumerableValue) return IsSingleObjectValid(value);

        // If object is an enumerable of items check if any item is not valid and if not return the first invalid item's
        // Validation results immediately
        foreach (var item in enumerableValue)
        {
            var itemValidationResult = IsSingleObjectValid(item);
            if (itemValidationResult != ValidationResult.Success) return itemValidationResult;
        }
        return ValidationResult.Success;
    }

    /// <summary>
    /// Checks if 1 object is valid, if not returns the validation results on it
    /// </summary>
    private ValidationResult? IsSingleObjectValid(object? value)
    {
        // If item is null - validation is automatically successful 
        if (value is null) return ValidationResult.Success;
        
        // Get all relevant value fields and make sure they are not null
        var enumerablePropertiesValues = new List<IEnumerable>();
        foreach (var enumerablePropertyName in _enumerablePropertiesNames)
        {
            var propertyInfo = value.GetType().GetProperty(enumerablePropertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (propertyInfo == null)
                throw new NotSupportedException(
                    $"Couldn't find public property {enumerablePropertyName} in {value.GetType().Name}");
            
            // If property is null treat it as an empty enumerable
            var propertyValue = propertyInfo.GetValue(value) ?? Enumerable.Empty<object>();
            if (propertyValue is not IEnumerable enumerable)
                throw new NotSupportedException(
                    $"Public property {enumerablePropertyName} in {value.GetType().Name} is not an IEnumerable");
            enumerablePropertiesValues.Add(enumerable);
        }
            
        var uniqueValues = new HashSet<object?>();
        foreach (var enumerable in enumerablePropertiesValues)
        {
            foreach(var item in enumerable)
            {   
                var propertyInfo = item.GetType().GetProperty(_fieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (propertyInfo == null)
                    throw new NotSupportedException(
                        $"Couldn't find public property {_fieldName} in {item.GetType().Name}");
                var fieldValue = propertyInfo.GetValue(item);
                if (uniqueValues.Contains(fieldValue))
                    return new ValidationResult(
                        $"Duplicate value `{fieldValue}` found for field `{_fieldName}` in enumerables " +
                        $"[{string.Join(", ", _enumerablePropertiesNames)}], {_validationErrorMeaningMessage}");
                
                uniqueValues.Add(fieldValue);
                
            }
        }
        return ValidationResult.Success;
    }
}