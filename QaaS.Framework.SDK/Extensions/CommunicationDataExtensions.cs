using System.Diagnostics.CodeAnalysis;
using QaaS.Framework.SDK.Session.CommunicationDataObjects;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.SDK.Extensions;

/// <summary>
/// Contains extensions for manipulating the CommunicationData objects
/// </summary>
public static class CommunicationDataExtensions
{
    private const string DefaultCommunicationDataType = "CommunicationData";

    /// <summary>
    /// Retrieves a CommunicationData from an enumerable of CommunicationData by its name
    /// </summary>
    /// <param name="communicationDataEnumerable"> The enumerable of CommunicationData </param>
    /// <param name="communicationDataName"> The name of the CommunicationData to search for in the CommunicationData enumerable </param>
    /// <param name="communicationDataType"> The type of the communication data (Inputs/Outputs)
    /// (if none is given calls it `CommunicationData`</param>
    /// <typeparam name="TData"> The Type of the data of the CommunicationData in the enumerable </typeparam>
    /// <returns> The CommunicationData that has the given name </returns>
    /// <exception cref="ArgumentException"> If less or more than 1 CommunicationData were found with the given name </exception>
    public static CommunicationData<TData> GetCommunicationDataByName<TData>
        (this IEnumerable<CommunicationData<TData>>? communicationDataEnumerable, string communicationDataName, 
            string? communicationDataType = null)
    {
        communicationDataType ??= DefaultCommunicationDataType;
        var itemsWithName = communicationDataEnumerable?.Where(communicationData =>
            communicationData.Name == communicationDataName).ToArray();
        
        if (itemsWithName == null || itemsWithName.Length < 1)
            throw new ArgumentException($"No {communicationDataType}" +
                                        $" by the name of {communicationDataName} was found.");
        if (itemsWithName.Length > 1)
            throw new ArgumentException($"More than 1 {communicationDataType} by the name" +
                                        $" of {communicationDataName} were found.");

        return itemsWithName.First();
    }
    
    /// <summary>
    /// Casts a CommunicationData to a different type.
    /// Null bodies always cast successfully and produce the default value of the target type
    /// (null for reference types, the zero value for value types).
    /// When a body is a deserialized representation of the target type instead of the target type itself
    /// (e.g. a JsonNode produced by json deserialization without a configured type), the cast automatically
    /// converts that body using the CommunicationData's own SerializationType when it has one, or the
    /// serialization type inferred from the body's runtime type otherwise
    /// (see <see cref="QaasSerializer.TryInferSerializationType"/>)
    /// </summary>
    /// <param name="communicationData"> The CommunicationData to cast </param>
    /// <param name="communicationDataType"> The type of the communication data (Inputs/Outputs)
    /// (if none is given calls it `CommunicationData`</param>
    /// <typeparam name="TCastTo"> The type to cast the CommunicationData to </typeparam>
    /// <returns> CommunicationData casted to the given type </returns>
    /// <exception cref="InvalidCastException"> If cast fails for any reason </exception>
    public static CommunicationData<TCastTo> CastCommunicationData<TCastTo>(this CommunicationData<object> communicationData, 
        string? communicationDataType = null)
    {
        communicationDataType ??= DefaultCommunicationDataType;
        return new CommunicationData<TCastTo>
        {
            Name = communicationData.Name,
            SerializationType = communicationData.SerializationType,
            Data = communicationData.Data.Select(item =>
            {
                try
                {
                    return item.CastObjectDetailedDataCore<TCastTo>(communicationData.SerializationType);
                }
                catch (Exception e)
                {
                    throw new InvalidCastException($"Failed to cast data item in {communicationDataType} " +
                                                   $"'{communicationData.Name}' to type {typeof(TCastTo)}.", e);
                }
            }).ToList()
        };
    }

    /// <summary>
    /// Retrieves data by its IoMatchIndex from a CommunicationData object
    /// </summary>
    /// <param name="communicationData"> The communicationData to retrieve the data from </param>
    /// <param name="ioMatchIndex"> The IoMatchIndex used to find the data </param>
    /// <returns> The first data with the given <see cref="ioMatchIndex"/> </returns>
    /// <exception cref="ArgumentException"> Thrown when no data with <see cref="ioMatchIndex"/> can be found
    /// </exception>
    public static DetailedData<TData> GetDataByIoMatchIndex<TData>(this CommunicationData<TData> communicationData,
        int ioMatchIndex) => 
        communicationData.Data.FirstOrDefault(data => data.MetaData?.IoMatchIndex == ioMatchIndex) ??
           throw new ArgumentException($"CommunicationData {communicationData.Name} does not contain" +
                                       $" a data item with {nameof(DetailedData<TData>.MetaData.IoMatchIndex)}" +
                                       $" {ioMatchIndex}");

    /// <summary>
    /// Attempts to retrieve a CommunicationData from an enumerable of CommunicationData by its name, never throws
    /// </summary>
    /// <param name="communicationDataEnumerable"> The enumerable of CommunicationData </param>
    /// <param name="communicationDataName"> The name of the CommunicationData to search for in the
    /// CommunicationData enumerable </param>
    /// <param name="communicationDataValue"> The CommunicationData with the given name when exactly one exists,
    /// null otherwise </param>
    /// <typeparam name="TData"> The Type of the data of the CommunicationData in the enumerable </typeparam>
    /// <returns> `true` if exactly 1 CommunicationData with the given name was found - else `false` </returns>
    /// <remarks>
    /// Example: `if (sessionData.Outputs.TryGetCommunicationDataByName("orders_output", out var output)) { ... }`
    /// </remarks>
    public static bool TryGetCommunicationDataByName<TData>(
        this IEnumerable<CommunicationData<TData>>? communicationDataEnumerable, string communicationDataName,
        [NotNullWhen(true)] out CommunicationData<TData>? communicationDataValue)
    {
        try
        {
            communicationDataValue = communicationDataEnumerable
                .GetCommunicationDataByName(communicationDataName);
            return true;
        }
        catch (ArgumentException)
        {
            communicationDataValue = null;
            return false;
        }
    }

    /// <summary>
    /// Attempts to cast a CommunicationData to a different type, never throws.
    /// Bodies that are deserialized representations of the target type (e.g. JsonNode) are automatically
    /// converted the same way <see cref="CastCommunicationData{TCastTo}"/> converts them
    /// </summary>
    /// <param name="communicationData"> The CommunicationData to cast </param>
    /// <param name="casted"> The casted CommunicationData when the cast succeeds, null otherwise </param>
    /// <typeparam name="TCastTo"> The type to cast the CommunicationData to </typeparam>
    /// <returns> `true` if the cast succeeded - else `false` </returns>
    /// <remarks>
    /// Example: `if (communication.TryCastCommunicationData&lt;byte[]&gt;(out var bytesCommunication)) { ... }`
    /// </remarks>
    public static bool TryCastCommunicationData<TCastTo>(this CommunicationData<object> communicationData,
        [NotNullWhen(true)] out CommunicationData<TCastTo>? casted)
    {
        try
        {
            casted = communicationData.CastCommunicationData<TCastTo>();
            return true;
        }
        catch (InvalidCastException)
        {
            casted = null;
            return false;
        }
    }

    /// <summary>
    /// Attempts to retrieve data by its IoMatchIndex from a CommunicationData object, never throws
    /// </summary>
    /// <param name="communicationData"> The communicationData to retrieve the data from </param>
    /// <param name="ioMatchIndex"> The IoMatchIndex used to find the data </param>
    /// <param name="data"> The first data with the given IoMatchIndex when one exists, null otherwise </param>
    /// <typeparam name="TData"> The Type of the data of the CommunicationData </typeparam>
    /// <returns> `true` if data with the given IoMatchIndex was found - else `false` </returns>
    /// <remarks>
    /// Example: `if (communication.TryGetDataByIoMatchIndex(0, out var firstMatch)) { ... }`
    /// </remarks>
    public static bool TryGetDataByIoMatchIndex<TData>(this CommunicationData<TData> communicationData,
        int ioMatchIndex, [NotNullWhen(true)] out DetailedData<TData>? data)
    {
        data = communicationData.Data.FirstOrDefault(item => item.MetaData?.IoMatchIndex == ioMatchIndex);
        return data != null;
    }

    /// <summary>
    /// Retrieves the bodies of all data items of a CommunicationData, removing the need to project the Data
    /// list manually when only the contents matter
    /// </summary>
    /// <param name="communicationData"> The CommunicationData to retrieve the bodies from </param>
    /// <typeparam name="TData"> The Type of the data of the CommunicationData </typeparam>
    /// <returns> The bodies of all data items, in their original order </returns>
    /// <remarks>
    /// Example: `IList&lt;object?&gt; bodies = communication.GetBodies();`
    /// </remarks>
    public static IList<TData?> GetBodies<TData>(this CommunicationData<TData> communicationData) =>
        communicationData.Data.Select(item => item.Body).ToList();

    /// <summary>
    /// Retrieves the bodies of all data items of a CommunicationData of type object directly as the requested
    /// type. Bodies that are deserialized representations of the target type (e.g. JsonNode bodies) are
    /// automatically converted using the CommunicationData's own SerializationType when it has one, or the
    /// serialization type inferred from each body's runtime type otherwise
    /// </summary>
    /// <param name="communicationData"> The CommunicationData to retrieve the bodies from </param>
    /// <typeparam name="TCasted"> The type to retrieve the bodies as </typeparam>
    /// <returns> The bodies of all data items typed as <typeparamref name="TCasted"/>, in their original
    /// order </returns>
    /// <exception cref="InvalidCastException"> If any body is not assignable to
    /// <typeparamref name="TCasted"/> and cannot be converted to it </exception>
    /// <remarks>
    /// Example: `IList&lt;string?&gt; bodies = communication.GetBodiesAs&lt;string&gt;();`
    /// </remarks>
    public static IList<TCasted?> GetBodiesAs<TCasted>(this CommunicationData<object> communicationData)
    {
        return communicationData.Data.Select((item, index) =>
        {
            try
            {
                return DataExtensions.GetBodyCore<TCasted>(item.Body, communicationData.SerializationType);
            }
            catch (InvalidCastException e)
            {
                throw new InvalidCastException(
                    $"Failed to retrieve the body of data item at index {index} in CommunicationData" +
                    $" '{communicationData.Name}' as type {typeof(TCasted)}.", e);
            }
        }).ToList();
    }

    /// <summary>
    /// Converts a CommunicationData of type object to a CommunicationData of the requested type regardless of
    /// the current representation of its bodies, using the CommunicationData's own SerializationType by default:
    /// bodies that already are <typeparamref name="TConverted"/> are kept as-is, byte[] bodies are deserialized,
    /// and any other representation (e.g. JsonNode, yaml dictionaries) is round-tripped through the
    /// serialization type into <typeparamref name="TConverted"/>.
    /// When no serialization type is available falls back to a plain cast
    /// (same behavior as <see cref="CastCommunicationData{TCastTo}"/>)
    /// </summary>
    /// <param name="communicationData"> The CommunicationData to convert </param>
    /// <param name="serializationTypeOverride"> The serialization type to convert with, if null is given the
    /// CommunicationData's own SerializationType is used </param>
    /// <typeparam name="TConverted"> The type to convert the CommunicationData to </typeparam>
    /// <returns> CommunicationData with all its data bodies converted to
    /// <typeparamref name="TConverted"/> and its SerializationType set to the serialization type the
    /// conversion actually used </returns>
    /// <exception cref="InvalidCastException"> If the conversion of any data item fails </exception>
    /// <remarks>
    /// Example: `CommunicationData&lt;Order&gt; typed = communication.ConvertCommunicationData&lt;Order&gt;();`
    /// </remarks>
    public static CommunicationData<TConverted> ConvertCommunicationData<TConverted>(
        this CommunicationData<object> communicationData, SerializationType? serializationTypeOverride = null)
    {
        var serializationType = serializationTypeOverride ?? communicationData.SerializationType;
        if (serializationType == null)
            return communicationData.CastCommunicationData<TConverted>();

        return new CommunicationData<TConverted>
        {
            Name = communicationData.Name,
            SerializationType = serializationType,
            Data = communicationData.Data.Select((item, index) =>
            {
                try
                {
                    return item.ConvertDetailedData<TConverted>(serializationType.Value);
                }
                catch (Exception e)
                {
                    throw new InvalidCastException(
                        $"Failed to convert data item at index {index} in CommunicationData" +
                        $" '{communicationData.Name}' to type {typeof(TConverted)}" +
                        $" using the {serializationType} serialization type.", e);
                }
            }).ToList()
        };
    }
}