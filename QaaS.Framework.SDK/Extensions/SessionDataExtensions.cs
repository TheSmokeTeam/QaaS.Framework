using QaaS.Framework.SDK.Session.CommunicationDataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;

namespace QaaS.Framework.SDK.Extensions;

/// <summary>
/// Contains extensions for manipulating the SessionData objects
/// </summary>
public static class SessionDataExtensions
{
    /// <summary>
    /// Retrieves a SessionData from an enumerable of SessionData by its name
    /// </summary>
    /// <param name="sessionDataEnumerable"> The enumerable of SessionData </param>
    /// <param name="sessionName"> The name of the session of the SessionData to search for in the SessionData enumerable </param>
    /// <typeparam name="TInput"> The Type of the input data of the SessionData in the enumerable </typeparam>
    /// <typeparam name="TOutput"> The Type of the output data of the SessionData in the enumerable </typeparam>
    /// <returns> The SessionData that has the given name </returns>
    /// <exception cref="ArgumentException"> If less or more than 1 SessionData were found with the given name </exception>
    public static GenericSessionData<TInput, TOutput> GetSessionDataByName<TInput, TOutput>
        (this IEnumerable<GenericSessionData<TInput, TOutput>>? sessionDataEnumerable, string sessionName)
    {
        var itemsWithName = sessionDataEnumerable?.Where(sessionData =>
            sessionData.Name == sessionName).ToArray();
        
        if (itemsWithName == null || itemsWithName.Length < 1)
            throw new ArgumentException($"No SessionData with the name '{sessionName}' was found.");
        if (itemsWithName.Length > 1)
            throw new ArgumentException($"More than 1 SessionData with the name '{sessionName}' were found.");

        return itemsWithName.First();
    }
    
    /// <summary>
    /// Retrieves an input CommunicationData from SessionData by its name
    /// </summary>
    /// <param name="sessionData"> The SessionData to look for the input in </param>
    /// <param name="inputName"> The name of the Input to search for in the SessionData </param>
    /// <typeparam name="TInput"> The Type of the input data of the SessionData </typeparam>
    /// <typeparam name="TOutput"> The Type of the output data of the SessionData </typeparam>
    /// <returns> The input CommunicationData that has the given name </returns>
    /// <exception cref="ArgumentException"> If less or more than 1 inputs were found with the given name </exception>
    public static CommunicationData<TInput> GetInputByName<TInput, TOutput>
    (this GenericSessionData<TInput, TOutput>? sessionData, string inputName)
    {
        return (sessionData?.Inputs).GetCommunicationDataByName(inputName, "Inputs");
    }
    
    /// <summary>
    /// Retrieves an input <see cref="CommunicationData{TData}"/> from <see cref="SessionData"/> by its name to inputValue
    /// </summary>
    /// <param name="sessionData"> The SessionData to look for the input in </param>
    /// <param name="inputName"> The name of the Input to search for in the SessionData </param>
    /// <typeparam name="TInput"> The Type of the input data of the SessionData </typeparam>
    /// <typeparam name="TOutput"> The Type of the output data of the SessionData </typeparam>
    /// <param name="inputValue"> The input CommunicationData that has the given name </param>
    /// <returns> Returns `true` if found - else `false` </returns>
    public static bool TryGetInputByName<TInput, TOutput>(this GenericSessionData<TInput, TOutput>? sessionData,
        string inputName, out CommunicationData<TInput>? inputValue)
    {
        try
        {
            inputValue = (sessionData?.Inputs).GetCommunicationDataByName(inputName, "Inputs");
            return true;
        }
        catch (ArgumentException)
        {
            inputValue = null;
            return false;
        }
    }

    /// <summary>
    /// Retrieves an output CommunicationData from SessionData by its name
    /// </summary>
    /// <param name="sessionData"> The SessionData to look for the output in </param>
    /// <param name="outputName"> The name of the Output to search for in the SessionData </param>
    /// <typeparam name="TInput"> The Type of the Output data of the SessionData </typeparam>
    /// <typeparam name="TOutput"> The Type of the output data of the SessionData </typeparam>
    /// <returns> The output CommunicationData that has the given name </returns>
    /// <exception cref="ArgumentException"> If less or more than 1 outputs were found with the given name </exception>
    public static CommunicationData<TOutput> GetOutputByName<TInput, TOutput>
        (this GenericSessionData<TInput, TOutput>? sessionData, string outputName)
    {
        return (sessionData?.Outputs).GetCommunicationDataByName(outputName, "Outputs");
    }
    
    /// <summary>
    /// Retrieves an input <see cref="CommunicationData{TData}"/> from <see cref="SessionData"/> by its name to inputValue
    /// </summary>
    /// <param name="sessionData"> The SessionData to look for the input in </param>
    /// <param name="outputName"> The name of the Input to search for in the SessionData </param>
    /// <typeparam name="TInput"> The Type of the input data of the SessionData </typeparam>
    /// <typeparam name="TOutput"> The Type of the output data of the SessionData </typeparam>
    /// <param name="outputValue"> The input CommunicationData that has the given name </param>
    /// <returns> Returns `true` if found - else `false` </returns>
    public static bool TryGetOutputByName<TInput, TOutput>(this GenericSessionData<TInput, TOutput>? sessionData,
        string outputName, out CommunicationData<TOutput>? outputValue)
    {
        try
        {
            outputValue = (sessionData?.Outputs).GetCommunicationDataByName(outputName, "Outputs");
            return true;
        }
        catch (ArgumentException)
        {
            outputValue = null;
            return false;
        }
    }

}
