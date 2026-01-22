using QaaS.Framework.SDK.Session.CommunicationDataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;

namespace QaaS.Framework.SDK.Extensions;

public static class RunningSessionDataExtensions
{
    /// <summary>
    /// Retrieves an input <see cref="RunningCommunicationData{TData}"/> from  <see cref="RunningSessionData{TInput,TOutput}"/> by its name
    /// </summary>
    /// <param name="runningSessionData"> The  <see cref="RunningSessionData{TInput,TOutput}"/> to look for the input in </param>
    /// <param name="inputName"> The name of the Input to search for in the  <see cref="RunningSessionData{TInput,TOutput}"/> </param>
    /// <typeparam name="TInput"> The Type of the Input data of the  <see cref="RunningSessionData{TInput,TOutput}"/> </typeparam>
    /// <typeparam name="TOutput"> The Type of the input data of the  <see cref="RunningSessionData{TInput,TOutput}"/> </typeparam>
    /// <returns> The input <see cref="RunningCommunicationData{TData}"/> that has the given name </returns>
    /// <exception cref="ArgumentException"> If less or more than 1 inputs were found with the given name </exception>
    public static RunningCommunicationData<TInput> GetInputByName<TInput, TOutput>(
        this RunningSessionData<TInput, TOutput>? runningSessionData, string inputName)
        => (runningSessionData?.Inputs).GetRunningCommunicationDataByName(inputName, "Inputs");


    /// <summary>
    /// Retrieves an output <see cref="RunningCommunicationData{TData}"/> from <see cref="RunningSessionData{TInput,TOutput}"/> by its name
    /// </summary>
    /// <param name="runningSessionData"> The <see cref="RunningSessionData{TInput,TOutput}"/> to look for the output in </param>
    /// <param name="outputName"> The name of the Output to search for in the <see cref="RunningSessionData{TInput,TOutput}"/> </param>
    /// <typeparam name="TInput"> The Type of the Output data of the <see cref="RunningSessionData{TInput,TOutput}"/> </typeparam>
    /// <typeparam name="TOutput"> The Type of the output data of the <see cref="RunningSessionData{TInput,TOutput}"/> </typeparam>
    /// <returns> The output <see cref="RunningCommunicationData{TData}"/> that has the given name </returns>
    /// <exception cref="ArgumentException"> If less or more than 1 outputs were found with the given name </exception>
    public static RunningCommunicationData<TOutput> GetOutputByName<TInput, TOutput>(
        this RunningSessionData<TInput, TOutput>? runningSessionData, string outputName)
        => (runningSessionData?.Outputs).GetRunningCommunicationDataByName(outputName, "Outputs");


    
}