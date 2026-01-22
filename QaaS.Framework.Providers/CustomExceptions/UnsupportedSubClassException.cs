namespace QaaS.Framework.Providers.CustomExceptions;

/// <summary>
/// Exception to throw when sub class is not a supported type of Parent class or interface 
/// </summary>
public class UnsupportedSubClassException(string subClassName, Type type) : Exception
{
    /// <inheritdoc />
    public override string Message => $"{subClassName} not a supported {type.Name}";
}
