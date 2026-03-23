using System.Collections.Immutable;

namespace QaaS.Framework.SDK.Extensions;

/// <summary>
/// Extensions for the IEnumerable interface
/// </summary>
public static class EnumerableExtensions
{
    /// <summary>
    /// Returns the single item contained in the provided sequence.
    /// </summary>
    /// <remarks>
    /// The helper enforces the invariant that exactly one item must be present and throws when the sequence is empty or contains more than one value.
    /// </remarks>
    /// <qaas-docs group="Utilities" subgroup="Enumerables" />
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
    /// Filters configuration objects by the supplied conditions and returns the matching items.
    /// </summary>
    /// <remarks>
    /// Throws when a requested condition does not match any item so callers can fail fast on invalid configuration references.
    /// </remarks>
    /// <qaas-docs group="Utilities" subgroup="Enumerables" />
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
