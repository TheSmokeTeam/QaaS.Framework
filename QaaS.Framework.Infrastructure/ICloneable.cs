namespace QaaS.Framework.Infrastructure;

/// <summary>
/// Produces an independent copy of the current instance.
/// </summary>
/// <remarks>
/// Mutations applied to the clone through the owning type's public API must not be observable on
/// the original. External resources handed to a builder at construction time (loggers, configuration
/// builders, DI containers) may be shared by reference — cloning only isolates state the builder
/// itself mutates.
/// </remarks>
public interface ICloneable<out T>
{
    T Clone();
}
