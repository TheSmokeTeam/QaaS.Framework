using System.ComponentModel.DataAnnotations;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace QaaS.Framework.Configurations.CustomValidationAttributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class YamlStringDeserializableAttribute : ValidationAttribute
{
    private readonly Type _targetType;

    public YamlStringDeserializableAttribute(Type targetType)
    {
        _targetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (value is not string yaml)
        {
            return new ValidationResult($"{validationContext.DisplayName} must be a YAML string.");
        }

        if (string.IsNullOrWhiteSpace(yaml))
        {
            return ValidationResult.Success;
        }

        try
        {
            var deserializer = new DeserializerBuilder().Build();
            deserializer.Deserialize(yaml, _targetType);
            return ValidationResult.Success;
        }
        catch (YamlException ex)
        {
            return new ValidationResult(
                $"{validationContext.DisplayName} must be valid YAML deserializable to {_targetType.Name}. {ex.Message}");
        }
    }
}
