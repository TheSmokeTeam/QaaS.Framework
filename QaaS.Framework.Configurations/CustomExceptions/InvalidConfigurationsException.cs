namespace QaaS.Framework.Configurations.CustomExceptions;

/// <summary>
/// Represents an error that occurs when invalid configuration was given, does not contain a stack trace
/// </summary>
public class InvalidConfigurationsException : Exception
{
    /// <summary>
    /// Empty stackTrace
    /// </summary>
    public override string StackTrace => "";

    /// <summary>
    /// Constructor 
    /// </summary>
    public InvalidConfigurationsException(string message) : base(message) { }
}