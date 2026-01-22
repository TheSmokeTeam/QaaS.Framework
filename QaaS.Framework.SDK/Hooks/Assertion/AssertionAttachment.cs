using QaaS.Framework.Serialization;

namespace QaaS.Framework.SDK.Hooks.Assertion;

/// <summary>
/// An object representing an attachment to store and display with an assertion 
/// </summary>
public record AssertionAttachment
{
    /// <summary>
    /// The path where the data will be stored. The path is relative to the test results directory.
    /// </summary>
    public string Path { get; set; }
    /// <summary>
    /// The actual data to store
    /// </summary>
    public object? Data { get; set; }
    /// <summary>
    /// How to serialize the data before storing it
    /// </summary>
    public SerializationType? SerializationType { get; set; }  
}