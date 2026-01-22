using System.Text.Json.Serialization;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.SDK.Session.CommunicationDataObjects;

/// <summary>
/// Base record that contains all bare minimum items to describe the result data of a communication action
/// </summary>
public abstract record BaseCommunicationData
{
    /// <summary>
    /// The name of the communication action that produces this communication data
    /// </summary>
    public string Name { get; init; } = null!;
    
    /// <summary>
    /// The serialization type that should be used to serialize this communication data
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SerializationType? SerializationType { get; init; }
    
}
