using System.Text;
using QaaS.Framework.Serialization.Serializers;
using IDeserializer = QaaS.Framework.Serialization.Deserializers.IDeserializer;

namespace QaaS.Framework.Serialization;

/// <summary>
/// Convenience extensions for <see cref="ISerializer"/> that provide string based and non-throwing serialization
/// </summary>
public static class SerializerExtensions
{
    /// <summary>
    /// Serializes the given data and returns the result as a UTF-8 string,
    /// most useful for the text based formats (Json, Yaml, Xml, XmlElement)
    /// </summary>
    /// <param name="serializer"> The serializer to serialize with </param>
    /// <param name="data"> The data to serialize, if null is given returns null </param>
    /// <returns> The serialized data decoded as a UTF-8 string </returns>
    /// <remarks>
    /// Example: `string? json = SerializationType.Json.BuildSerializer().SerializeToString(order);`
    /// </remarks>
    public static string? SerializeToString(this ISerializer serializer, object? data)
    {
        var serialized = serializer.Serialize(data);
        return serialized is null ? null : Encoding.UTF8.GetString(serialized);
    }

    /// <summary>
    /// Attempts to serialize the given data, never throws
    /// </summary>
    /// <param name="serializer"> The serializer to serialize with </param>
    /// <param name="data"> The data to serialize </param>
    /// <param name="serialized"> The serialized data when serialization succeeds, null otherwise </param>
    /// <returns> `true` if serialization succeeded - else `false` </returns>
    /// <remarks>
    /// Example: `if (serializer.TrySerialize(order, out var payload)) { ... }`
    /// </remarks>
    public static bool TrySerialize(this ISerializer serializer, object? data, out byte[]? serialized)
    {
        try
        {
            serialized = serializer.Serialize(data);
            return true;
        }
        catch
        {
            serialized = null;
            return false;
        }
    }
}

/// <summary>
/// Convenience extensions for <see cref="IDeserializer"/> that provide typed,
/// string based and non-throwing deserialization
/// </summary>
public static class DeserializerExtensions
{
    /// <summary>
    /// Deserializes the given byte[] directly to <typeparamref name="TResult"/> instead of object,
    /// removing the need to pass a Type instance and cast the result manually
    /// </summary>
    /// <param name="deserializer"> The deserializer to deserialize with </param>
    /// <param name="data"> The serialized data, if null is given returns default </param>
    /// <typeparam name="TResult"> The type to deserialize to </typeparam>
    /// <returns> The deserialized data typed as <typeparamref name="TResult"/> </returns>
    /// <exception cref="QaasSerializationException"> If the deserialized data is not assignable to
    /// <typeparamref name="TResult"/> </exception>
    /// <remarks>
    /// Example: `Order? order = deserializer.Deserialize&lt;Order&gt;(payload);`
    /// </remarks>
    public static TResult? Deserialize<TResult>(this IDeserializer deserializer, byte[]? data)
    {
        var deserialized = deserializer.Deserialize(data, typeof(TResult));
        if (deserialized is null) return default;
        if (deserialized is TResult typed) return typed;
        throw new QaasSerializationException(
            $"`{deserializer.GetType().Name}` deserialization produced `{deserialized.GetType()}` which is not" +
            $" assignable to the requested type `{typeof(TResult)}`");
    }

    /// <summary>
    /// Deserializes the given UTF-8 string directly to <typeparamref name="TResult"/>,
    /// most useful for the text based formats (Json, Yaml, Xml, XmlElement)
    /// </summary>
    /// <param name="deserializer"> The deserializer to deserialize with </param>
    /// <param name="data"> The serialized data as a UTF-8 string, if null is given returns default </param>
    /// <typeparam name="TResult"> The type to deserialize to </typeparam>
    /// <returns> The deserialized data typed as <typeparamref name="TResult"/> </returns>
    /// <exception cref="QaasSerializationException"> If the deserialized data is not assignable to
    /// <typeparamref name="TResult"/> </exception>
    /// <remarks>
    /// Example: `Order? order = SerializationType.Json.BuildDeserializer().DeserializeFromString&lt;Order&gt;(json);`
    /// </remarks>
    public static TResult? DeserializeFromString<TResult>(this IDeserializer deserializer, string? data) =>
        deserializer.Deserialize<TResult>(data is null ? null : Encoding.UTF8.GetBytes(data));

    /// <summary>
    /// Deserializes the given UTF-8 string to an object,
    /// most useful for the text based formats (Json, Yaml, Xml, XmlElement)
    /// </summary>
    /// <param name="deserializer"> The deserializer to deserialize with </param>
    /// <param name="data"> The serialized data as a UTF-8 string, if null is given returns null </param>
    /// <param name="deserializeType"> The C# type to deserialize to, if null is given deserializes to the
    /// deserializer's default C# object </param>
    /// <returns> The deserialized object from the given data </returns>
    /// <remarks>
    /// Example: `object? parsed = deserializer.DeserializeFromString(json, typeof(Order));`
    /// </remarks>
    public static object? DeserializeFromString(this IDeserializer deserializer, string? data,
        Type? deserializeType = null) =>
        deserializer.Deserialize(data is null ? null : Encoding.UTF8.GetBytes(data), deserializeType);

    /// <summary>
    /// Attempts to deserialize the given byte[] directly to <typeparamref name="TResult"/>, never throws
    /// </summary>
    /// <param name="deserializer"> The deserializer to deserialize with </param>
    /// <param name="data"> The serialized data </param>
    /// <param name="deserialized"> The deserialized data when deserialization succeeds, default otherwise </param>
    /// <typeparam name="TResult"> The type to deserialize to </typeparam>
    /// <returns> `true` if deserialization succeeded - else `false` </returns>
    /// <remarks>
    /// Example: `if (deserializer.TryDeserialize&lt;Order&gt;(payload, out var order)) { ... }`
    /// </remarks>
    public static bool TryDeserialize<TResult>(this IDeserializer deserializer, byte[]? data,
        out TResult? deserialized)
    {
        try
        {
            deserialized = deserializer.Deserialize<TResult>(data);
            return true;
        }
        catch
        {
            deserialized = default;
            return false;
        }
    }
}
