namespace QaaS.Framework.Serialization.Serializers;

/// <summary>
/// Binary serialisation is disabled for security.
/// BinaryFormatter is a well-known remote code-execution vector and has been removed
/// from the .NET 9+ BCL. Serializing via BinaryFormatter is forbidden.
/// Use a different <see cref="SerializationType"/> (Json, MessagePack, etc.) instead.
/// </summary>
public class Binary : ISerializer
{
    /// <inheritdoc />
    public byte[]? Serialize(object? data)
    {
        if (data is null)
            return null;

        throw new NotSupportedException(
            "BinaryFormatter serialization is disabled for security. "
                + "BinaryFormatter is an unauthenticated RCE sink and has been removed from .NET 9+. "
                + "Configure a safe SerializationType (e.g. Json, MessagePack) instead."
        );
    }
}
