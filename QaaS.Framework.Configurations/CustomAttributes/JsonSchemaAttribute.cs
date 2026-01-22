namespace QaaS.Framework.Configurations.CustomAttributes;

/// <summary>
/// Attribute used to define what classes get added to the final JSON schema
/// Non-abstract classes that have this attribute will be automatically added to the JSON schema
/// Classes that inherit from base classes that have this attribute will also have the attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class JsonSchemaAttribute : Attribute;