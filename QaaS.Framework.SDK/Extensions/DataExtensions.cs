using QaaS.Framework.SDK.Session;
using QaaS.Framework.SDK.Session.DataObjects;

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
}