using System.Diagnostics.CodeAnalysis;
using QaaS.Framework.SDK.Session.CommunicationDataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;
using QaaS.Framework.Serialization;

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

    /// <summary>
    /// Retrieves an input CommunicationData from SessionData by its name with all its data bodies converted to
    /// the requested type in a single call, using the input's own SerializationType by default
    /// (see <see cref="CommunicationDataExtensions.ConvertCommunicationData{TConverted}"/> for the conversion
    /// rules)
    /// </summary>
    /// <param name="sessionData"> The SessionData to look for the input in </param>
    /// <param name="inputName"> The name of the Input to search for in the SessionData </param>
    /// <param name="serializationTypeOverride"> The serialization type to convert with, if null is given the
    /// input's own SerializationType is used </param>
    /// <typeparam name="TInput"> The type to convert the input's data bodies to </typeparam>
    /// <returns> The input CommunicationData that has the given name, typed as
    /// <typeparamref name="TInput"/> </returns>
    /// <exception cref="ArgumentException"> If less or more than 1 inputs were found with the given name </exception>
    /// <exception cref="InvalidCastException"> If the conversion of any data item fails </exception>
    /// <remarks>
    /// Example: `CommunicationData&lt;OrderRequest&gt; input = sessionData.GetInputAs&lt;OrderRequest&gt;("orders_input");`
    /// </remarks>
    public static CommunicationData<TInput> GetInputAs<TInput>(
        this GenericSessionData<object, object>? sessionData, string inputName,
        SerializationType? serializationTypeOverride = null) =>
        sessionData.GetInputByName(inputName).ConvertCommunicationData<TInput>(serializationTypeOverride);

    /// <summary>
    /// Retrieves an output CommunicationData from SessionData by its name with all its data bodies converted to
    /// the requested type in a single call, using the output's own SerializationType by default
    /// (see <see cref="CommunicationDataExtensions.ConvertCommunicationData{TConverted}"/> for the conversion
    /// rules)
    /// </summary>
    /// <param name="sessionData"> The SessionData to look for the output in </param>
    /// <param name="outputName"> The name of the Output to search for in the SessionData </param>
    /// <param name="serializationTypeOverride"> The serialization type to convert with, if null is given the
    /// output's own SerializationType is used </param>
    /// <typeparam name="TOutput"> The type to convert the output's data bodies to </typeparam>
    /// <returns> The output CommunicationData that has the given name, typed as
    /// <typeparamref name="TOutput"/> </returns>
    /// <exception cref="ArgumentException"> If less or more than 1 outputs were found with the given name </exception>
    /// <exception cref="InvalidCastException"> If the conversion of any data item fails </exception>
    /// <remarks>
    /// Example: `CommunicationData&lt;Order&gt; output = sessionData.GetOutputAs&lt;Order&gt;("orders_output");`
    /// </remarks>
    public static CommunicationData<TOutput> GetOutputAs<TOutput>(
        this GenericSessionData<object, object>? sessionData, string outputName,
        SerializationType? serializationTypeOverride = null) =>
        sessionData.GetOutputByName(outputName).ConvertCommunicationData<TOutput>(serializationTypeOverride);

    /// <summary>
    /// Attempts to retrieve an input CommunicationData from SessionData by its name with all its data bodies
    /// converted to the requested type, never throws
    /// </summary>
    /// <param name="sessionData"> The SessionData to look for the input in </param>
    /// <param name="inputName"> The name of the Input to search for in the SessionData </param>
    /// <param name="inputValue"> The typed input CommunicationData when retrieval and conversion succeed,
    /// null otherwise </param>
    /// <param name="serializationTypeOverride"> The serialization type to convert with, if null is given the
    /// input's own SerializationType is used </param>
    /// <typeparam name="TInput"> The type to convert the input's data bodies to </typeparam>
    /// <returns> `true` if the input was found and converted - else `false` </returns>
    /// <remarks>
    /// Example: `if (sessionData.TryGetInputAs&lt;OrderRequest&gt;("orders_input", out var input)) { ... }`
    /// </remarks>
    public static bool TryGetInputAs<TInput>(this GenericSessionData<object, object>? sessionData,
        string inputName, [NotNullWhen(true)] out CommunicationData<TInput>? inputValue,
        SerializationType? serializationTypeOverride = null)
    {
        try
        {
            inputValue = sessionData.GetInputAs<TInput>(inputName, serializationTypeOverride);
            return true;
        }
        catch (Exception e) when (e is ArgumentException or InvalidCastException)
        {
            inputValue = null;
            return false;
        }
    }

    /// <summary>
    /// Attempts to retrieve an output CommunicationData from SessionData by its name with all its data bodies
    /// converted to the requested type, never throws
    /// </summary>
    /// <param name="sessionData"> The SessionData to look for the output in </param>
    /// <param name="outputName"> The name of the Output to search for in the SessionData </param>
    /// <param name="outputValue"> The typed output CommunicationData when retrieval and conversion succeed,
    /// null otherwise </param>
    /// <param name="serializationTypeOverride"> The serialization type to convert with, if null is given the
    /// output's own SerializationType is used </param>
    /// <typeparam name="TOutput"> The type to convert the output's data bodies to </typeparam>
    /// <returns> `true` if the output was found and converted - else `false` </returns>
    /// <remarks>
    /// Example: `if (sessionData.TryGetOutputAs&lt;Order&gt;("orders_output", out var output)) { ... }`
    /// </remarks>
    public static bool TryGetOutputAs<TOutput>(this GenericSessionData<object, object>? sessionData,
        string outputName, [NotNullWhen(true)] out CommunicationData<TOutput>? outputValue,
        SerializationType? serializationTypeOverride = null)
    {
        try
        {
            outputValue = sessionData.GetOutputAs<TOutput>(outputName, serializationTypeOverride);
            return true;
        }
        catch (Exception e) when (e is ArgumentException or InvalidCastException)
        {
            outputValue = null;
            return false;
        }
    }

    /// <summary>
    /// Retrieves the typed bodies of all data items of an input found by its name in a single call,
    /// the shortest path from a SessionData to the actual contents of an input
    /// (see <see cref="GetInputAs{TInput}"/> for the conversion rules)
    /// </summary>
    /// <param name="sessionData"> The SessionData to look for the input in </param>
    /// <param name="inputName"> The name of the Input to search for in the SessionData </param>
    /// <param name="serializationTypeOverride"> The serialization type to convert with, if null is given the
    /// input's own SerializationType is used </param>
    /// <typeparam name="TInput"> The type to convert the input's data bodies to </typeparam>
    /// <returns> The bodies of all the input's data items typed as <typeparamref name="TInput"/>,
    /// in their original order </returns>
    /// <exception cref="ArgumentException"> If less or more than 1 inputs were found with the given name </exception>
    /// <exception cref="InvalidCastException"> If the conversion of any data item fails </exception>
    /// <remarks>
    /// Example: `IList&lt;OrderRequest?&gt; requests = sessionData.GetInputBodies&lt;OrderRequest&gt;("orders_input");`
    /// </remarks>
    public static IList<TInput?> GetInputBodies<TInput>(this GenericSessionData<object, object>? sessionData,
        string inputName, SerializationType? serializationTypeOverride = null) =>
        sessionData.GetInputAs<TInput>(inputName, serializationTypeOverride).GetBodies();

    /// <summary>
    /// Retrieves the typed bodies of all data items of an output found by its name in a single call,
    /// the shortest path from a SessionData to the actual contents of an output
    /// (see <see cref="GetOutputAs{TOutput}"/> for the conversion rules)
    /// </summary>
    /// <param name="sessionData"> The SessionData to look for the output in </param>
    /// <param name="outputName"> The name of the Output to search for in the SessionData </param>
    /// <param name="serializationTypeOverride"> The serialization type to convert with, if null is given the
    /// output's own SerializationType is used </param>
    /// <typeparam name="TOutput"> The type to convert the output's data bodies to </typeparam>
    /// <returns> The bodies of all the output's data items typed as <typeparamref name="TOutput"/>,
    /// in their original order </returns>
    /// <exception cref="ArgumentException"> If less or more than 1 outputs were found with the given name </exception>
    /// <exception cref="InvalidCastException"> If the conversion of any data item fails </exception>
    /// <remarks>
    /// Example: `IList&lt;Order?&gt; orders = sessionData.GetOutputBodies&lt;Order&gt;("orders_output");`
    /// </remarks>
    public static IList<TOutput?> GetOutputBodies<TOutput>(this GenericSessionData<object, object>? sessionData,
        string outputName, SerializationType? serializationTypeOverride = null) =>
        sessionData.GetOutputAs<TOutput>(outputName, serializationTypeOverride).GetBodies();

}
