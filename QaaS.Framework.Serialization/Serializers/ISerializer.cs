namespace QaaS.Framework.Serialization.Serializers;

/// <summary>
/// Represents a class that provides serialize functionality for a specific type
/// </summary>
public interface ISerializer
{
    /// <summary>
    /// Serializes the given object data according to the class's supported serialization
    /// </summary>
    /// <param name="data"> Deserialized data </param>
    /// <returns> The object serialized </returns>
    public byte[]? Serialize(object? data);
}
