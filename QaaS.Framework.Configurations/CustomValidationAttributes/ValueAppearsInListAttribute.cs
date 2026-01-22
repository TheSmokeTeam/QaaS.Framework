using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

/// <summary>
/// Validates that all values in a specified property (which must be an enumerable) on each item
/// in an enumerable are present in the list of values from another specified property
/// across all items in the parent enumerable.
/// 
/// Example usage:
/// [ValueAppearsInListAttribute(nameof(SessionBuilder.Stage), nameof(SessionBuilder.RunUntilStage))]
/// public SessionBuilder[] Sessions { get; set; } = [];
/// 
/// This ensures that every value in RunUntilStage is one of the Stage values across all sessions.
/// </summary>
public class ValueAppearsInListAttribute : ValidationAttribute
{
    private readonly string _listPropertyName; // The property to create the list from

    private readonly string _propertyInListName; // The property to check that exists in the list

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueAppearsInListAttribute"/> class.
    /// </summary>
    /// <param name="listPropertyName">The property to create the list from</param>
    /// <param name="propertyInListName">The property to check that exists in the list</param>
    public ValueAppearsInListAttribute(string listPropertyName, string propertyInListName)
    {
        _listPropertyName = listPropertyName ?? throw new ArgumentNullException(nameof(listPropertyName));
        _propertyInListName =
            propertyInListName ?? throw new ArgumentNullException(nameof(propertyInListName));
    }

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // If value is null or not an enumerable, validation passes (assuming optional)
        if (value is not IEnumerable enumerable)
            return ValidationResult.Success;

        // Collect all values to put into the list
        var listValues = (from object? item in enumerable
            let propertyInfo =
                item.GetType().GetProperty(_listPropertyName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static) ??
                throw new ArgumentException(
                    $"Could not find property '{_listPropertyName}' on type '{item.GetType().FullName}'")
            select propertyInfo.GetValue(item)).OfType<object>().ToList();

        // Now validate each item's enumerable property
        foreach (var item in enumerable)
        {
            var itemToCheckProperty =
                item.GetType().GetProperty(_propertyInListName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                ?? throw new ArgumentException(
                    $"Could not find property '{_propertyInListName}' on type '{item.GetType().FullName}'");

            var itemValue = itemToCheckProperty.GetValue(item);
            if (itemValue == null)
                continue;
            // Check if this value exists in the list of valid values
            var validValues = string.Join(", ", listValues.Distinct().Select(v => v.ToString() ?? "null"));
            if (!listValues.Contains(itemValue))
                return new ValidationResult(
                    $"{_propertyInListName} must equal one of the values of {_listPropertyName}, but '{itemValue}' doesn't. Valid values are: [{validValues}]",
                    [validationContext.MemberName!]
                );
        }

        return ValidationResult.Success;
    }
}