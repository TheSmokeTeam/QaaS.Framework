using System.Collections.Immutable;

namespace QaaS.Framework.SDK.Extensions;

/// <summary>
/// Extensions for the IEnumerable interface
/// </summary>
public static class EnumerableExtensions
{
    /// <summary>
    /// Validates the enumerable only consists of one item, if not throws indicative exception, if yes returns that one
    /// item
    /// </summary>
    /// <param name="enumerable"> The enumerable to validate and return as a single item </param>
    /// <typeparam name="TItem"> The type of the items in the enumerable </typeparam>
    /// <returns> The single item in the enumerable </returns>
    public static TItem AsSingle<TItem>(this IEnumerable<TItem> enumerable)
    {
        if (enumerable == null)
            throw new ArgumentNullException(nameof(enumerable), 
                $"The enumerable of type {typeof(TItem)} requested as a single item is null.");
        
        using var enumerator = enumerable.GetEnumerator();
        if (!enumerator.MoveNext())
            throw new ArgumentException(
                $"The enumerable of type {typeof(TItem)} requested as a single item contains no items.");
        
        var singleItem = enumerator.Current;
        
        if (enumerator.MoveNext())
            throw new ArgumentException(
                $"The enumerable of type {typeof(TItem)} requested as a single item contains more than 1 item.");

        return singleItem;
    }

    /// <summary>
    /// Returns only the data objects from 'dataList' that pass the given filter.
    /// </summary>
    /// 
    /// <param name="dataList"> The list of data to search for datas that have
    /// a field matching an item in the `conditionFieldItemEnumerable` </param>
    /// <param name="conditionFieldItemEnumerable"> A list of filter patterns to filter against a field
    /// in the data in the `dataList` </param>
    /// <param name="filter"> The filter function used to filter the dataList </param>
    /// <param name="nameOfDataList"> The name of the list of data given for usage in the exception thrown when
    /// none of its datas match an item from the `conditionFieldItemEnumerable` by the equation function</param>
    /// 
    /// <typeparam name="TData"> The type of the data </typeparam>
    /// <typeparam name="TPattern"> The type of the filter pattern </typeparam>
    /// <returns> A list of data matching the given `conditionFieldItemEnumerable` according to the equation function </returns>
    /// 
    /// <exception cref="ArgumentException"> Thrown when an item from `conditionFieldItemEnumerable` does not match
    /// any of the datas in the `dataList` according to the equation function</exception>
    public static IList<TData> GetFilteredConfigurationObjectList<TData, TPattern>(
             IImmutableList<TData> dataList,
             IEnumerable<TPattern>? conditionFieldItemEnumerable, 
             Func<TData, TPattern, bool> filter,
             string nameOfDataList)
    {
        var datasWithConditionFieldItem = new List<TData>();
        if (conditionFieldItemEnumerable == null)
            return new List<TData>();
        foreach (var conditionFieldItem in conditionFieldItemEnumerable)
        {
            var dataForConditionFieldItem = dataList.Where(data =>
                filter.Invoke(data, conditionFieldItem));
            
            var dataAsArray = dataForConditionFieldItem.ToArray();
            // throw exception if data matching conditionFieldItem not existing
            if (dataAsArray.Length == 0)
                throw new ArgumentException(
                    $"Item {conditionFieldItem} not found in {nameOfDataList}");
            datasWithConditionFieldItem.AddRange(dataAsArray);
        }

        return datasWithConditionFieldItem;
    }
}