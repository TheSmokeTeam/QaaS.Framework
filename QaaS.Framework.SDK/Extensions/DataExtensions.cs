using System.Diagnostics.CodeAnalysis;
using QaaS.Framework.SDK.Session;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.SDK.Extensions;

/// <summary>
/// Contains extensions for manipulating the Data objects
/// </summary>
public static class DataExtensions
{
    /// <summary>
    /// Casts a `Data` of type object to another type, if the cast is not valid will throw InvalidCastException
    /// </summary>
    /// <param name="data"> The Data to cast to another type </param>
    /// <typeparam name="TCasted"> The type to cast to </typeparam>
    /// <returns> Data casted to the cast type </returns>
    public static Data<TCasted> CastObjectData<TCasted>(this Data<object> data)
    {
        try
        {
            return new Data<TCasted>
            {
                Body = (TCasted?)data.Body,
                MetaData = data.MetaData
            };
        }
        catch (Exception e)
        {
            throw new InvalidCastException($"Failed to cast `Data<object>` that is actually " +
                                           $"`Data<{data.Body?.GetType()}>` to `Data<{typeof(TCasted)}>`", e);
        }
    }
    
    /// <summary>
    /// Casts a `Data` of any type to a Data of type object, if the cast is not valid will throw InvalidCastException
    /// </summary>
    /// <param name="data"> The Data to cast to Data object </param>
    /// <typeparam name="TData"> The type to cast from </typeparam>
    /// <returns> Data casted to object </returns>
    public static Data<object> CastToObjectData<TData>(this Data<TData> data)
    {
        return new Data<object>
        {
            Body = data.Body,
            MetaData = data.MetaData
        };
    }
    
    /// <summary>
    /// Casts a `DetailedData` of type object to another type, if the cast is not valid will throw InvalidCastException
    /// </summary>
    /// <param name="detailedData"> The DetailedData to cast to another type </param>
    /// <typeparam name="TCasted"> The type to cast to </typeparam>
    /// <returns> DetailedData casted to the cast type </returns>
    public static DetailedData<TCasted> CastObjectDetailedData<TCasted>(this DetailedData<object> detailedData)
    {
        try
        {
            return new DetailedData<TCasted>
            {
                Body = (TCasted?)detailedData.Body,
                MetaData = detailedData.MetaData,
                Timestamp = detailedData.Timestamp
            };
        }
        catch (Exception e)
        {
            throw new InvalidCastException($"Failed to cast `DetailedData<object>` that is actually " +
                                          $"`DetailedData<{detailedData.Body?.GetType()}>` to `DetailedData<{typeof(TCasted)}>`",
                e);
        }
    }
    
    /// <summary>
    /// Casts a `DetailedData` of any type to a DetailedData of type object, if the cast is not valid will throw InvalidCastException
    /// </summary>
    /// <param name="detailedData"> The DetailedData to cast to DetailedData object </param>
    /// <typeparam name="TData"> The type to cast from </typeparam>
    /// <returns> DetailedData casted to object</returns>
    public static DetailedData<object> CastToObjectDetailedData<TData>(this DetailedData<TData> detailedData)
    {
        return new DetailedData<object>
        {
            Body = detailedData.Body,
            MetaData = detailedData.MetaData,
            Timestamp = detailedData.Timestamp
        };
    }
    
    /// <summary>
    /// Filters the data of a detailed data item according to the given DataFilter
    /// </summary>
    public static DetailedData<TData> FilterData<TData>(this DetailedData<TData> dataItemToFilter,
        DataFilter dataFilter) where TData : class
    {
        return dataItemToFilter with 
        {
            Body = dataFilter.Body ? dataItemToFilter.Body : null,
            Timestamp = dataFilter.Timestamp ? dataItemToFilter.Timestamp : null,
            MetaData = dataFilter.MetaData ? dataItemToFilter.MetaData : null
        };
    }

    /// <summary>
    /// Attempts to cast a `Data` of type object to another type, never throws
    /// </summary>
    /// <param name="data"> The Data to cast to another type </param>
    /// <param name="casted"> The casted Data when the cast succeeds, null otherwise </param>
    /// <typeparam name="TCasted"> The type to cast to </typeparam>
    /// <returns> `true` if the cast succeeded - else `false` </returns>
    /// <remarks>
    /// Example: `if (data.TryCastObjectData&lt;byte[]&gt;(out var bytesData)) { ... }`
    /// </remarks>
    public static bool TryCastObjectData<TCasted>(this Data<object> data,
        [NotNullWhen(true)] out Data<TCasted>? casted)
    {
        try
        {
            casted = data.CastObjectData<TCasted>();
            return true;
        }
        catch (InvalidCastException)
        {
            casted = null;
            return false;
        }
    }

    /// <summary>
    /// Attempts to cast a `DetailedData` of type object to another type, never throws
    /// </summary>
    /// <param name="detailedData"> The DetailedData to cast to another type </param>
    /// <param name="casted"> The casted DetailedData when the cast succeeds, null otherwise </param>
    /// <typeparam name="TCasted"> The type to cast to </typeparam>
    /// <returns> `true` if the cast succeeded - else `false` </returns>
    /// <remarks>
    /// Example: `if (detailedData.TryCastObjectDetailedData&lt;byte[]&gt;(out var bytesItem)) { ... }`
    /// </remarks>
    public static bool TryCastObjectDetailedData<TCasted>(this DetailedData<object> detailedData,
        [NotNullWhen(true)] out DetailedData<TCasted>? casted)
    {
        try
        {
            casted = detailedData.CastObjectDetailedData<TCasted>();
            return true;
        }
        catch (InvalidCastException)
        {
            casted = null;
            return false;
        }
    }

    /// <summary>
    /// Retrieves the body of a `Data` (or `DetailedData`) of type object directly as the requested type,
    /// removing the need to cast the whole Data wrapper in order to reach a typed body
    /// </summary>
    /// <param name="data"> The Data to read the body from </param>
    /// <typeparam name="TBody"> The type to retrieve the body as </typeparam>
    /// <returns> The body typed as <typeparamref name="TBody"/>, or default when the body is null </returns>
    /// <exception cref="InvalidCastException"> If the body is not assignable to
    /// <typeparamref name="TBody"/> </exception>
    /// <remarks>
    /// Example: `byte[]? raw = detailedData.GetBodyAs&lt;byte[]&gt;();`
    /// </remarks>
    public static TBody? GetBodyAs<TBody>(this Data<object> data)
    {
        return data.Body switch
        {
            null => default,
            TBody typed => typed,
            _ => throw new InvalidCastException(
                $"The body of this `Data<object>` is of type `{data.Body.GetType()}` which is not assignable" +
                $" to the requested type `{typeof(TBody)}`. If the body is a different representation of the" +
                $" same content (e.g. JsonNode or byte[]), use `ConvertBodyTo<{typeof(TBody).Name}>` with the" +
                " matching SerializationType instead")
        };
    }

    /// <summary>
    /// Attempts to retrieve the body of a `Data` (or `DetailedData`) of type object directly as the requested
    /// type, never throws
    /// </summary>
    /// <param name="data"> The Data to read the body from </param>
    /// <param name="body"> The typed body when the cast succeeds (default when the body is null),
    /// default otherwise </param>
    /// <typeparam name="TBody"> The type to retrieve the body as </typeparam>
    /// <returns> `true` if the body is null or assignable to <typeparamref name="TBody"/> - else `false` </returns>
    /// <remarks>
    /// Example: `if (detailedData.TryGetBodyAs&lt;string&gt;(out var text)) { ... }`
    /// </remarks>
    public static bool TryGetBodyAs<TBody>(this Data<object> data, out TBody? body)
    {
        switch (data.Body)
        {
            case null:
                body = default;
                return true;
            case TBody typed:
                body = typed;
                return true;
            default:
                body = default;
                return false;
        }
    }

    /// <summary>
    /// Converts the body of a `Data` (or `DetailedData`) of type object to the requested type regardless of its
    /// current representation: bodies that already are <typeparamref name="TBody"/> are returned as-is, byte[]
    /// bodies are deserialized, and any other representation (e.g. JsonNode, yaml dictionaries) is round-tripped
    /// through the given serialization type into <typeparamref name="TBody"/>
    /// </summary>
    /// <param name="data"> The Data whose body to convert </param>
    /// <param name="serializationType"> The serialization type that describes the body's content format </param>
    /// <typeparam name="TBody"> The type to convert the body to </typeparam>
    /// <returns> The body converted to <typeparamref name="TBody"/>, or default when the body is null </returns>
    /// <exception cref="QaasSerializationException"> If the conversion fails </exception>
    /// <remarks>
    /// Example: `Order? order = detailedData.ConvertBodyTo&lt;Order&gt;(SerializationType.Json);`
    /// </remarks>
    public static TBody? ConvertBodyTo<TBody>(this Data<object> data, SerializationType serializationType)
    {
        var body = data.Body;
        if (body is null) return default;
        if (body is TBody typed) return typed;
        var serializedBody = body as byte[] ?? QaasSerializer.Serialize(body, serializationType);
        return QaasSerializer.Deserialize<TBody>(serializedBody, serializationType);
    }

    /// <summary>
    /// Converts a `Data` of type object to a `Data` of the requested type regardless of its current body
    /// representation, preserving its MetaData (see <see cref="ConvertBodyTo{TBody}"/> for the conversion rules)
    /// </summary>
    /// <param name="data"> The Data to convert </param>
    /// <param name="serializationType"> The serialization type that describes the body's content format </param>
    /// <typeparam name="TBody"> The type to convert the Data to </typeparam>
    /// <returns> Data with its body converted to <typeparamref name="TBody"/> </returns>
    /// <exception cref="QaasSerializationException"> If the conversion fails </exception>
    /// <remarks>
    /// Example: `Data&lt;Order&gt; typed = data.ConvertData&lt;Order&gt;(SerializationType.Json);`
    /// </remarks>
    public static Data<TBody> ConvertData<TBody>(this Data<object> data, SerializationType serializationType)
    {
        return new Data<TBody>
        {
            Body = data.ConvertBodyTo<TBody>(serializationType),
            MetaData = data.MetaData
        };
    }

    /// <summary>
    /// Converts a `DetailedData` of type object to a `DetailedData` of the requested type regardless of its
    /// current body representation, preserving its MetaData and Timestamp
    /// (see <see cref="ConvertBodyTo{TBody}"/> for the conversion rules)
    /// </summary>
    /// <param name="detailedData"> The DetailedData to convert </param>
    /// <param name="serializationType"> The serialization type that describes the body's content format </param>
    /// <typeparam name="TBody"> The type to convert the DetailedData to </typeparam>
    /// <returns> DetailedData with its body converted to <typeparamref name="TBody"/> </returns>
    /// <exception cref="QaasSerializationException"> If the conversion fails </exception>
    /// <remarks>
    /// Example: `DetailedData&lt;Order&gt; typed = detailedData.ConvertDetailedData&lt;Order&gt;(SerializationType.Json);`
    /// </remarks>
    public static DetailedData<TBody> ConvertDetailedData<TBody>(this DetailedData<object> detailedData,
        SerializationType serializationType)
    {
        return new DetailedData<TBody>
        {
            Body = detailedData.ConvertBodyTo<TBody>(serializationType),
            MetaData = detailedData.MetaData,
            Timestamp = detailedData.Timestamp
        };
    }
}