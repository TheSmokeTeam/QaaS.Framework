using QaaS.Framework.SDK.Session.CommunicationDataObjects;
using QaaS.Framework.SDK.Session.DataObjects;

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
    /// Casts a CommunicationData to a different type
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
                    return item.CastObjectDetailedData<TCastTo>();
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
    
}