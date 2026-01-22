namespace QaaS.Framework.Configurations.CustomExceptions;

/// <summary>
/// Represents an error that occurs when the configuration requested could not be found
/// </summary>
public class CouldNotFindConfigurationException : Exception
{
    /// <summary>
    /// Constructor 
    /// </summary>
    public CouldNotFindConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}