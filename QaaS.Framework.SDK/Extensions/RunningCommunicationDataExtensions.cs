using QaaS.Framework.SDK.Session.CommunicationDataObjects;

namespace QaaS.Framework.SDK.Extensions;

/// <summary>
/// Contains extensions for manipulating the RunningCommunicationData objects
/// </summary>
public static class RunningCommunicationDataExtensions
{
    private const string DefaultRunningCommunicationDataType = "RunningCommunicationData";

    /// <summary>
    /// Retrieves a <see cref="RunningCommunicationData{TData}"/> from an enumerable of <see cref="RunningCommunicationData{TData}"/> by its name
    /// </summary>
    /// <param name="runningCommunicationDataEnumerable"> The enumerable of <see cref="RunningCommunicationData{TData}"/> </param>
    /// <param name="runningCommunicationDataName"> The name of the <see cref="RunningCommunicationData{TData}"/> to search for in the <see cref="RunningCommunicationData{TData}"/> enumerable </param>
    /// <param name="runningCommunicationDataType"> The type of the <see cref="RunningCommunicationData{TData}"/> (Inputs/Outputs) 
    /// (if none is given calls it `CommunicationData`</param>
    /// <typeparam name="TData"> The Type of the data of the <see cref="RunningCommunicationData{TData}"/> in the enumerable </typeparam>
    /// <returns> The <see cref="RunningCommunicationData{TData}"/> that has the given name </returns>
    /// <exception cref="ArgumentException"> If less or more than 1 <see cref="RunningCommunicationData{TData}"/> were found with the given name </exception>
    public static RunningCommunicationData<TData> GetRunningCommunicationDataByName<TData>
    (this IEnumerable<RunningCommunicationData<TData>>? runningCommunicationDataEnumerable,
        string runningCommunicationDataName, string? runningCommunicationDataType = null)
    {
        runningCommunicationDataType ??= DefaultRunningCommunicationDataType;
        var itemsWithName = runningCommunicationDataEnumerable?.Where(communicationData =>
            communicationData.Name == runningCommunicationDataName).ToArray();

        if (itemsWithName == null || itemsWithName.Length < 1)
            throw new ArgumentException($"No {runningCommunicationDataType}" +
                                        $" by the name of {runningCommunicationDataName} was found.");
        if (itemsWithName.Length > 1)
            throw new ArgumentException($"More than 1 {runningCommunicationDataType} by the name" +
                                        $" of {runningCommunicationDataName} were found.");

        return itemsWithName.First();
    }
    
}