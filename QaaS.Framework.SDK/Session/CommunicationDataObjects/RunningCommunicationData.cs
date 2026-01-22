using System.Collections.Concurrent;
using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Framework.SDK.Session.CommunicationDataObjects;

/// <summary>
/// Represents an action that is currently running
/// </summary>
public record RunningCommunicationData<TData> : BaseCommunicationData
{
    /// <summary>
    /// The real-time data of the currently running action
    /// </summary>
    public BlockingCollection<DetailedData<TData>?> Data { get; } = new();

    /// <summary>
    /// The real-time data queued
    /// </summary>
    public ConcurrentQueue<DetailedData<TData>?> Queue { get; } = new();

    /// <summary>
    /// A cancellation token for the real time data, if cancelled the running action will crash
    /// </summary>
    public CancellationTokenSource DataCancellationTokenSource { get; set; } = new();


    /// <summary>
    /// Retrieves all the data produced as a result of the communication action, in a lazy form
    /// </summary>
    public virtual IEnumerable<DetailedData<TData>> GetData()
    {
        var consumedItems = 0;
        foreach (var _ in Data.GetConsumingEnumerable(DataCancellationTokenSource.Token)
                     .Where(detailedData => detailedData != null))
        {
            var newAddedItemsCount = Queue.Count - consumedItems;
            var newAddedItems = Queue.Skip(consumedItems).Take(newAddedItemsCount);
            foreach (var item in newAddedItems.Where(item => item != null))
                yield return item!;

            consumedItems += newAddedItemsCount;
        }

        foreach (var item in Queue.Skip(consumedItems).Where(item => item != null))
            yield return item!;
    }
    
}