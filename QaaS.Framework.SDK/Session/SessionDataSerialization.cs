using System.Text.Json;
using QaaS.Framework.SDK.Session.CommunicationDataObjects;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.SDK.Session;

/// <summary>
/// Contains serialization functionality for the SessionData objects
/// </summary>
public static class  SessionDataSerialization
{
    /// <summary>
    /// Deserializes a SessionData object from a byte[] representation of json with consideration to all of its
    /// serialization configurations
    /// </summary>
    public static SessionData DeserializeSessionData(byte[] serializedData,
        JsonSerializerOptions? options = null)
    {
        var serializedSessionData = JsonSerializer.Deserialize<SerializedSessionData>(serializedData, options)
                                    ?? throw new ArgumentException("Received null when attempting to deserialize session data");
        return new SessionData
        {
            Name = serializedSessionData.Name,
            UtcStartTime = serializedSessionData.UtcStartTime,
            UtcEndTime = serializedSessionData.UtcEndTime,
            SessionFailures = serializedSessionData.SessionFailures,
            Inputs = serializedSessionData.Inputs?.Select(DeserializeCommunicationData).ToList(),
            Outputs = serializedSessionData.Outputs?.Select(DeserializeCommunicationData).ToList()
        };
    }
    
    /// <summary>
    /// Deserialize all of the data within a serialized communication data object according to its serialization type and configurations
    /// </summary>
    public static CommunicationData<object>  DeserializeCommunicationData(
        SerializedCommunicationData serializedCommunicationData)
    {
        var deserializer = DeserializerFactory.BuildDeserializer(serializedCommunicationData.SerializationType);
        var deserializedCommunicationData = new CommunicationData<object> 
        {
            Name = serializedCommunicationData.Name,
            SerializationType = serializedCommunicationData.SerializationType
        };
        
        if (deserializer == null) 
            return deserializedCommunicationData with
            {
                Data = serializedCommunicationData.Data.Select(item => new DetailedData<object>
                {
                    MetaData = item.MetaData,
                    Timestamp = item.Timestamp,
                    Body = item.Body
                }).ToList()
            };
        return deserializedCommunicationData with { Data = serializedCommunicationData.Data.Select(item => 
            new DetailedData<object>
            {
                MetaData = item.MetaData,
                Timestamp = item.Timestamp, 
                Body = deserializer.Deserialize(item.Body, item.Type?.GetConfiguredType()) 
            }).ToList()
        };
    }
    
    /// <summary>
    /// Serializes a SessionData object to a byte[] representation of json with consideration to all of its
    /// serialization configurations
    /// </summary>
    public static byte[] SerializeSessionData(SessionData sessionData, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new SerializedSessionData
        {
            Name = sessionData.Name,
            UtcStartTime = sessionData.UtcStartTime,
            UtcEndTime = sessionData.UtcEndTime,
            SessionFailures = sessionData.SessionFailures,
            Inputs = sessionData.Inputs?.Select(SerializeCommunicationData).ToList(),
            Outputs = sessionData.Outputs?.Select(SerializeCommunicationData).ToList()
        }, options);
    }

    /// <summary>
    /// Serializes all of the data within a communication data object according to its serialization type and configurations
    /// </summary>
    public static SerializedCommunicationData SerializeCommunicationData(CommunicationData<object> communicationData)
    {
        var serializer = SerializerFactory.BuildSerializer(communicationData.SerializationType);
        var serializedCommunicationData = new SerializedCommunicationData
        {
            Name = communicationData.Name,
            SerializationType = communicationData.SerializationType
        };
        
        if (serializer == null) 
            return serializedCommunicationData with
            {
                Data = communicationData.Data.Select(item => new SerializedDetailedData
                {
                    MetaData = item.MetaData,
                    Timestamp = item.Timestamp,
                    Body = (byte[]?)item.Body
                }).ToList()
            };
        return serializedCommunicationData with { Data = communicationData.Data.Select(item =>
            {
                var bodyType = item.Body?.GetType();
                return new SerializedDetailedData
                {
                    MetaData = item.MetaData,
                    Timestamp = item.Timestamp,
                    Body = serializer.Serialize(item.Body),
                    Type = bodyType == null
                        ? null 
                        : new SpecificTypeConfig
                        {
                            AssemblyName = bodyType.Assembly.FullName,
                            TypeFullName = bodyType.FullName
                        }
                };
            }).ToList()
        };
    }
}