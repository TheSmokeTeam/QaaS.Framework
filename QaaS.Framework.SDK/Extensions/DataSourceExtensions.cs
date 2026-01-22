using System.Collections.Immutable;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;

namespace QaaS.Framework.SDK.Extensions;

/// <summary>
/// Contains extensions for manipulating the DataSource objects
/// </summary>
public static class DataSourceExtensions
{
    /// <summary>
    /// Retrieves a DataSource from an enumerable of DataSources by its name
    /// </summary>
    /// <param name="dataSourceEnumerable"> The enumerable of DataSources </param>
    /// <param name="dataSourceName"> The name of the DataSource to search for in the DataSources enumerable </param>
    /// <returns> The DataSource that has the given name </returns>
    /// <exception cref="ArgumentException"> If less or more than 1 DataSources were found with the given name </exception>
    public static DataSource GetDataSourceByName
        (this IEnumerable<DataSource>? dataSourceEnumerable, string dataSourceName)
    {
        var itemsWithName = dataSourceEnumerable?.Where(dataSource =>
            dataSource.Name == dataSourceName).ToArray();
        
        if (itemsWithName == null || itemsWithName.Length < 1)
            throw new ArgumentException($"No DataSource by the name of '{dataSourceName}' was found.");
        if (itemsWithName.Length > 1)
            throw new ArgumentException($"More than 1 DataSources by the name of '{dataSourceName}' were found.");

        return itemsWithName.First();
    }

    /// <summary>
    /// Casts a DataSource to a different type
    /// </summary>
    /// <param name="dataSource"> The DataSource to generate and cast </param>
    /// <param name="ranSessionsDataList"> A list of the data of sessions already ran before this DataSource was called,
    /// by default no sessions are passed </param>
    /// <typeparam name="TCastTo"> The type to cast the DataSource to </typeparam>
    /// <returns> DataSource casted to the given type </returns>
    /// <exception cref="InvalidCastException"> If cast fails for any reason </exception>
    public static IEnumerable<Data<TCastTo>> RetrieveAndCast<TCastTo>
        (this DataSource dataSource, IImmutableList<SessionData>? ranSessionsDataList = null)
    {
        return dataSource.Retrieve(ranSessionsDataList).Select(retrievedData =>
        {
            try
            {
                return retrievedData.CastObjectData<TCastTo>();
            }
            catch (Exception e)
            {
                throw new InvalidCastException($"Failed to cast data generated from data source " +
                                               $"`{dataSource.Name}` to type {typeof(TCastTo)}.", e);
            }
        });
    }
}