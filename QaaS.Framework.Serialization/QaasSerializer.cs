using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using QaaS.Framework.Serialization.Serializers;
using IDeserializer = QaaS.Framework.Serialization.Deserializers.IDeserializer;

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
        ISerializer? serializer;
        try
        {
            serializer = SerializerFactory.BuildSerializer(serializationType);
        }
        catch (Exception e)
        {
            throw new QaasSerializationException(
                $"Failed to build a serializer for serialization type `{serializationType}`."
                    + " See InnerException for the original failure",
                e
            );
        }

        if (serializer == null)
            return data switch
            {
                null => null,
                byte[] raw => raw,
                _ => throw new QaasSerializationException(
                    $"No serialization type was given (null) so the data can only pass through as-is, which"
                        + $" requires it to already be a `byte[]`, but the given data is of type `{data.GetType()}`."
                        + $" Provide a SerializationType (e.g. SerializationType.Json) to serialize this object"
                ),
            };

        try
        {
            return serializer.Serialize(data);
        }
        catch (Exception e)
        {
            throw new QaasSerializationException(
                $"Failed to serialize data of type `{data?.GetType().ToString() ?? "null"}`"
                    + $" as {serializationType}. See InnerException for the original failure",
                e
            );
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
    public static bool TrySerialize(
        object? data,
        SerializationType? serializationType,
        out byte[]? serialized
    )
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
    public static object? Deserialize(
        byte[]? data,
        SerializationType? serializationType,
        Type? deserializeType = null
    )
    {
        IDeserializer? deserializer;
        try
        {
            deserializer = DeserializerFactory.BuildDeserializer(serializationType);
        }
        catch (Exception e)
        {
            throw new QaasSerializationException(
                $"Failed to build a deserializer for serialization type `{serializationType}`."
                    + " See InnerException for the original failure",
                e
            );
        }

        if (deserializer == null)
            return data;

        try
        {
            return deserializer.Deserialize(data, deserializeType);
        }
        catch (Exception e)
        {
            throw new QaasSerializationException(
                $"Failed to deserialize {data?.Length.ToString() ?? "null"} bytes as {serializationType}"
                    + $"{(deserializeType == null ? string.Empty : $" into type `{deserializeType}`")}."
                    + " See InnerException for the original failure",
                e
            );
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
                    $"No serialization type was given (null) so the data can only pass through as-is, which"
                        + $" requires the requested type to be `byte[]` (or compatible), but `{typeof(TResult)}`"
                        + $" was requested. Provide a SerializationType (e.g. SerializationType.Json) to deserialize"
                ),
            };

        var deserialized = Deserialize(data, serializationType, typeof(TResult));
        if (deserialized is null)
            return default;
        if (deserialized is TResult result)
            return result;
        throw new QaasSerializationException(
            $"{serializationType} deserialization produced `{deserialized.GetType()}` which is not assignable"
                + $" to the requested type `{typeof(TResult)}`"
        );
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
    public static TResult? DeserializeFromString<TResult>(
        string? data,
        SerializationType? serializationType
    ) =>
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
    public static object? DeserializeFromString(
        string? data,
        SerializationType? serializationType,
        Type? deserializeType = null
    ) =>
        Deserialize(
            data is null ? null : Encoding.UTF8.GetBytes(data),
            serializationType,
            deserializeType
        );

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
    public static bool TryDeserialize<TResult>(
        byte[]? data,
        SerializationType? serializationType,
        out TResult? deserialized
    )
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
    public static bool TryDeserializeFromString<TResult>(
        string? data,
        SerializationType? serializationType,
        out TResult? deserialized
    )
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

    /// <summary>
    /// Attempts to infer the <see cref="SerializationType"/> that produced the given deserialized body from
    /// its runtime representation: JsonNode/JsonElement/JsonDocument bodies are Json, XDocument bodies are
    /// Xml, XElement bodies are XmlElement, and the generic dictionaries/lists produced by untyped yaml (and
    /// map formatted MessagePack) deserialization are treated as Yaml.
    /// Representations that are ambiguous (e.g. byte[], string, object[]) are not inferred
    /// </summary>
    /// <param name="body"> The deserialized body whose serialization type to infer </param>
    /// <param name="inferred"> The inferred serialization type when inference succeeds </param>
    /// <returns> `true` if the serialization type could be inferred from the body - else `false` </returns>
    /// <remarks>
    /// Example: `if (QaasSerializer.TryInferSerializationType(body, out var serializationType)) { ... }`
    /// </remarks>
    public static bool TryInferSerializationType(object? body, out SerializationType inferred)
    {
        switch (body)
        {
            case JsonNode or JsonElement or JsonDocument:
                inferred = SerializationType.Json;
                return true;
            case XDocument:
                inferred = SerializationType.Xml;
                return true;
            case XElement:
                inferred = SerializationType.XmlElement;
                return true;
            case Dictionary<object, object> or List<object>:
                inferred = SerializationType.Yaml;
                return true;
            default:
                inferred = default;
                return false;
        }
    }
}
