namespace QaaS.Framework.SDK.Session.MetaDataObjects;

/// <summary>
/// Represents the metadata of an http message (either request or response)
/// </summary>
public record Http
{
    public int? StatusCode { get; init; }
    
    public string? ReasonPhrase { get; init; }
    
    public string? Version { get; init; }
    
    public Uri? Uri { get; init; }  
    
    /// <summary>
    /// The http message's content headers
    /// </summary>
    public IDictionary<string, string>? Headers { get; init; }
    
    public IDictionary<string, string>? RequestHeaders { get; init; }
    
    public IDictionary<string, string>? ResponseHeaders { get; init; }

    public IDictionary<string, string>? TrailingHeaders { get; init; }
    
    /// <summary>
    /// The http message's parameters
    /// </summary>
    public IDictionary<string, string>? PathParameters { get; set; }
}