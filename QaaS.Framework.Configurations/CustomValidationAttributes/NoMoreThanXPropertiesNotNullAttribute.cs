using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

/// <summary>
/// Validates no more than a X properties are not null, if no properties are given
/// checks all properties in the object
/// checks the properties of the object the attribute is put over
/// </summary>
public class NoMoreThanXPropertiesNotNullAttribute : ValidationAttribute
{
    private readonly IEnumerable<string>? _propertyNamesToCheck;
    private readonly int _maximumNumberOfPropertiesNotNull;

    /// <summary>
    /// Initiate attribute with a few specific properties to check and not the whole object
    /// </summary>
    /// <param name="propertyNamesToCheckArray"> An array of string representing a list of properties
    /// Those are the properties this attribute will check.
    /// </param>
    /// <param name="maximumNumberOfPropertiesNotNull"> The maximum number of properties
    /// that are not null in the given object</param>
    public NoMoreThanXPropertiesNotNullAttribute(string[] propertyNamesToCheckArray,
        int maximumNumberOfPropertiesNotNull = 1)
    {
        _propertyNamesToCheck = propertyNamesToCheckArray;
        _maximumNumberOfPropertiesNotNull = maximumNumberOfPropertiesNotNull;
    }

    /// <summary>
    /// Initiates attribute that checks all properties in the object
    /// </summary>
    public NoMoreThanXPropertiesNotNullAttribute(int maximumNumberOfPropertiesNotNull = 1)
    {
        _propertyNamesToCheck = null;
        _maximumNumberOfPropertiesNotNull = maximumNumberOfPropertiesNotNull;
    }

    /// <inheritdoc />
    protected override ValidationResult? IsValid([NotNull] object value, ValidationContext validationContext)
    {
        var valueType = value.GetType();
        var allObjectProperties = valueType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        // If there are no specific properties to check, check them all
        if (_propertyNamesToCheck == null)
        {
            return allObjectProperties.Count(property => property.GetValue(value) != null)
                   <= _maximumNumberOfPropertiesNotNull
                ? ValidationResult.Success
                : new ValidationResult(
                    $"More than {_maximumNumberOfPropertiesNotNull} properties in {valueType.Name} are not null");
        }

        var specificPropertiesToCheck = allObjectProperties.Where(
            property => _propertyNamesToCheck.Contains(property.Name)).ToArray();

        return specificPropertiesToCheck.Count(property => property.GetValue(value) != null)
               <= _maximumNumberOfPropertiesNotNull
            ? ValidationResult.Success
            : new ValidationResult(
                "Out of the following properties: " +
                $"[{string.Join(", ", specificPropertiesToCheck.Select(property => property.Name))}] " +
                $"in {valueType.Name} More than {_maximumNumberOfPropertiesNotNull} are not null");
    }
}