namespace QaaS.Framework.Serialization.Deserializers;

/// <summary>
/// Represents a class that provides deserialize functionality for a specific type
/// </summary>
public interface IDeserializer
{
    /// <summary>
    /// Deserializes the given byte[] data according to the class's supported serialization to an object
    /// </summary>
    /// <param name="data"> Serialized data </param>
    /// <param name="deserializeType"> The C# type to deserialize to </param>
    /// <returns> The deserialized object from the given data </returns>
    public object? Deserialize(byte[]? data, Type? deserializeType);
}
