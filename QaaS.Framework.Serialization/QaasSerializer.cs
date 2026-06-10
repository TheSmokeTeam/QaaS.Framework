using System.Text;

namespace QaaS.Framework.Serialization;

/// <summary>
/// One-stop static facade for QaaS serialization that turns the usual
/// "build factory, deserialize to object, pass a Type, cast the result" dance into a single indicative call,
/// e.g. `QaasSerializer.Deserialize&lt;Order&gt;(bytes, SerializationType.Json)`.
/// All failures are reported as <see cref="QaasSerializationException"/> with an indicative message.
/// A null serialization type mirrors the framework's pass-through semantics: data is treated as raw byte[]
/// </summary>
public static class QaasSerializer
{
    /// <summary>
    /// Serializes the given data with the given serialization type in a single call
    /// </summary>
    /// <param name="data"> The data to serialize, if null is given returns null </param>
    /// <param name="serializationType"> The serialization type to serialize with, if null is given the data
    /// passes through as-is and must already be a byte[] (or null) </param>
    /// <returns> The serialized data </returns>
    /// <exception cref="QaasSerializationException"> If serialization fails or if no serialization type was
    /// given and the data is not already a byte[] </exception>
    /// <remarks>
    /// Example: `byte[]? payload = QaasSerializer.Serialize(order, SerializationType.Json);`
    /// </remarks>
    public static byte[]? Serialize(object? data, SerializationType? serializationType)
    {
        var serializer = SerializerFactory.BuildSerializer(serializationType);
        if (serializer == null)
            return data switch
            {
                null => null,
                byte[] raw => raw,
                _ => throw new QaasSerializationException(
                    $"No serialization type was given (null) so the data can only pass through as-is, which" +
                    $" requires it to already be a `byte[]`, but the given data is of type `{data.GetType()}`." +
                    $" Provide a SerializationType (e.g. SerializationType.Json) to serialize this object")
            };

        try
        {
            return serializer.Serialize(data);
        }
        catch (Exception e)
        {
            throw new QaasSerializationException(
                $"Failed to serialize data of type `{data?.GetType().ToString() ?? "null"}`" +
                $" as {serializationType}. See InnerException for the original failure", e);
        }
    }

    /// <summary>
    /// Serializes the given data with the given serialization type and returns the result as a UTF-8 string,
    /// most useful for the text based formats (Json, Yaml, Xml, XmlElement)
    /// </summary>
    /// <param name="data"> The data to serialize, if null is given returns null </param>
    /// <param name="serializationType"> The serialization type to serialize with </param>
    /// <returns> The serialized data decoded as a UTF-8 string </returns>
    /// <exception cref="QaasSerializationException"> If serialization fails </exception>
    /// <remarks>
    /// Example: `string? json = QaasSerializer.SerializeToString(order, SerializationType.Json);`
    /// </remarks>
    public static string? SerializeToString(object? data, SerializationType? serializationType)
    {
        var serialized = Serialize(data, serializationType);
        return serialized is null ? null : Encoding.UTF8.GetString(serialized);
    }

    /// <summary>
    /// Attempts to serialize the given data with the given serialization type, never throws
    /// </summary>
    /// <param name="data"> The data to serialize </param>
    /// <param name="serializationType"> The serialization type to serialize with </param>
    /// <param name="serialized"> The serialized data when serialization succeeds, null otherwise </param>
    /// <returns> `true` if serialization succeeded - else `false` </returns>
    /// <remarks>
    /// Example: `if (QaasSerializer.TrySerialize(order, SerializationType.Json, out var payload)) { ... }`
    /// </remarks>
    public static bool TrySerialize(object? data, SerializationType? serializationType, out byte[]? serialized)
    {
        try
        {
            serialized = Serialize(data, serializationType);
            return true;
        }
        catch
        {
            serialized = null;
            return false;
        }
    }

    /// <summary>
    /// Deserializes the given byte[] with the given serialization type in a single call
    /// </summary>
    /// <param name="data"> The serialized data, if null is given returns null </param>
    /// <param name="serializationType"> The serialization type to deserialize with, if null is given the data
    /// passes through as-is </param>
    /// <param name="deserializeType"> The C# type to deserialize to, if null is given deserializes to the
    /// deserializer's default C# object </param>
    /// <returns> The deserialized object from the given data </returns>
    /// <exception cref="QaasSerializationException"> If deserialization fails </exception>
    /// <remarks>
    /// Example: `object? parsed = QaasSerializer.Deserialize(payload, SerializationType.Json, typeof(Order));`
    /// </remarks>
    public static object? Deserialize(byte[]? data, SerializationType? serializationType,
        Type? deserializeType = null)
    {
        var deserializer = DeserializerFactory.BuildDeserializer(serializationType);
        if (deserializer == null) return data;

        try
        {
            return deserializer.Deserialize(data, deserializeType);
        }
        catch (Exception e)
        {
            throw new QaasSerializationException(
                $"Failed to deserialize {data?.Length.ToString() ?? "null"} bytes as {serializationType}" +
                $"{(deserializeType == null ? string.Empty : $" into type `{deserializeType}`")}." +
                " See InnerException for the original failure", e);
        }
    }

    /// <summary>
    /// Deserializes the given byte[] with the given serialization type directly to
    /// <typeparamref name="TResult"/> in a single call
    /// </summary>
    /// <param name="data"> The serialized data, if null is given returns default </param>
    /// <param name="serializationType"> The serialization type to deserialize with, if null is given the data
    /// passes through as-is and must already be assignable to <typeparamref name="TResult"/> </param>
    /// <typeparam name="TResult"> The type to deserialize to </typeparam>
    /// <returns> The deserialized data typed as <typeparamref name="TResult"/> </returns>
    /// <exception cref="QaasSerializationException"> If deserialization fails or the deserialized data is not
    /// assignable to <typeparamref name="TResult"/> </exception>
    /// <remarks>
    /// Example: `Order? order = QaasSerializer.Deserialize&lt;Order&gt;(payload, SerializationType.Json);`
    /// </remarks>
    public static TResult? Deserialize<TResult>(byte[]? data, SerializationType? serializationType)
    {
        if (serializationType == null)
            return data switch
            {
                null => default,
                TResult typed => typed,
                _ => throw new QaasSerializationException(
                    $"No serialization type was given (null) so the data can only pass through as-is, which" +
                    $" requires the requested type to be `byte[]` (or compatible), but `{typeof(TResult)}`" +
                    $" was requested. Provide a SerializationType (e.g. SerializationType.Json) to deserialize")
            };

        var deserialized = Deserialize(data, serializationType, typeof(TResult));
        if (deserialized is null) return default;
        if (deserialized is TResult result) return result;
        throw new QaasSerializationException(
            $"{serializationType} deserialization produced `{deserialized.GetType()}` which is not assignable" +
            $" to the requested type `{typeof(TResult)}`");
    }

    /// <summary>
    /// Deserializes the given UTF-8 string with the given serialization type directly to
    /// <typeparamref name="TResult"/>, most useful for the text based formats (Json, Yaml, Xml, XmlElement)
    /// </summary>
    /// <param name="data"> The serialized data as a UTF-8 string, if null is given returns default </param>
    /// <param name="serializationType"> The serialization type to deserialize with </param>
    /// <typeparam name="TResult"> The type to deserialize to </typeparam>
    /// <returns> The deserialized data typed as <typeparamref name="TResult"/> </returns>
    /// <exception cref="QaasSerializationException"> If deserialization fails or the deserialized data is not
    /// assignable to <typeparamref name="TResult"/> </exception>
    /// <remarks>
    /// Example: `Order? order = QaasSerializer.DeserializeFromString&lt;Order&gt;(json, SerializationType.Json);`
    /// </remarks>
    public static TResult? DeserializeFromString<TResult>(string? data, SerializationType? serializationType) =>
        Deserialize<TResult>(data is null ? null : Encoding.UTF8.GetBytes(data), serializationType);

    /// <summary>
    /// Deserializes the given UTF-8 string with the given serialization type,
    /// most useful for the text based formats (Json, Yaml, Xml, XmlElement)
    /// </summary>
    /// <param name="data"> The serialized data as a UTF-8 string, if null is given returns null </param>
    /// <param name="serializationType"> The serialization type to deserialize with </param>
    /// <param name="deserializeType"> The C# type to deserialize to, if null is given deserializes to the
    /// deserializer's default C# object </param>
    /// <returns> The deserialized object from the given data </returns>
    /// <exception cref="QaasSerializationException"> If deserialization fails </exception>
    /// <remarks>
    /// Example: `object? parsed = QaasSerializer.DeserializeFromString(json, SerializationType.Json);`
    /// </remarks>
    public static object? DeserializeFromString(string? data, SerializationType? serializationType,
        Type? deserializeType = null) =>
        Deserialize(data is null ? null : Encoding.UTF8.GetBytes(data), serializationType, deserializeType);

    /// <summary>
    /// Attempts to deserialize the given byte[] with the given serialization type directly to
    /// <typeparamref name="TResult"/>, never throws
    /// </summary>
    /// <param name="data"> The serialized data </param>
    /// <param name="serializationType"> The serialization type to deserialize with </param>
    /// <param name="deserialized"> The deserialized data when deserialization succeeds, default otherwise </param>
    /// <typeparam name="TResult"> The type to deserialize to </typeparam>
    /// <returns> `true` if deserialization succeeded - else `false` </returns>
    /// <remarks>
    /// Example: `if (QaasSerializer.TryDeserialize&lt;Order&gt;(payload, SerializationType.Json, out var order)) { ... }`
    /// </remarks>
    public static bool TryDeserialize<TResult>(byte[]? data, SerializationType? serializationType,
        out TResult? deserialized)
    {
        try
        {
            deserialized = Deserialize<TResult>(data, serializationType);
            return true;
        }
        catch
        {
            deserialized = default;
            return false;
        }
    }

    /// <summary>
    /// Attempts to deserialize the given UTF-8 string with the given serialization type directly to
    /// <typeparamref name="TResult"/>, never throws
    /// </summary>
    /// <param name="data"> The serialized data as a UTF-8 string </param>
    /// <param name="serializationType"> The serialization type to deserialize with </param>
    /// <param name="deserialized"> The deserialized data when deserialization succeeds, default otherwise </param>
    /// <typeparam name="TResult"> The type to deserialize to </typeparam>
    /// <returns> `true` if deserialization succeeded - else `false` </returns>
    /// <remarks>
    /// Example: `if (QaasSerializer.TryDeserializeFromString&lt;Order&gt;(json, SerializationType.Json, out var order)) { ... }`
    /// </remarks>
    public static bool TryDeserializeFromString<TResult>(string? data, SerializationType? serializationType,
        out TResult? deserialized)
    {
        try
        {
            deserialized = DeserializeFromString<TResult>(data, serializationType);
            return true;
        }
        catch
        {
            deserialized = default;
            return false;
        }
    }
}
