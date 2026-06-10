using QaaS.Framework.Serialization.Serializers;
using IDeserializer = QaaS.Framework.Serialization.Deserializers.IDeserializer;

namespace QaaS.Framework.Serialization;

/// <summary>
/// Discoverability extensions for <see cref="SerializationType"/> that allow building serializers and
/// deserializers directly from the enum value, e.g. `SerializationType.Json.BuildSerializer()`
/// </summary>
public static class SerializationTypeExtensions
{
    /// <summary>
    /// Builds the serializer that matches this serialization type
    /// </summary>
    /// <param name="serializationType"> The serialization type to build a serializer for </param>
    /// <returns> The built serializer </returns>
    /// <exception cref="ArgumentOutOfRangeException"> If the serialization type is not supported </exception>
    /// <remarks>
    /// Example: `var serializer = SerializationType.Yaml.BuildSerializer();`
    /// </remarks>
    public static ISerializer BuildSerializer(this SerializationType serializationType) =>
        SerializerFactory.BuildSerializer(serializationType)!;

    /// <summary>
    /// Builds the deserializer that matches this serialization type
    /// </summary>
    /// <param name="serializationType"> The serialization type to build a deserializer for </param>
    /// <returns> The built deserializer </returns>
    /// <exception cref="ArgumentOutOfRangeException"> If the serialization type is not supported </exception>
    /// <remarks>
    /// Example: `var deserializer = SerializationType.Yaml.BuildDeserializer();`
    /// </remarks>
    public static IDeserializer BuildDeserializer(this SerializationType serializationType) =>
        DeserializerFactory.BuildDeserializer(serializationType)!;
}
