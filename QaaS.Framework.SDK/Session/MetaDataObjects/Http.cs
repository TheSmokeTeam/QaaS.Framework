namespace QaaS.Framework.SDK.Session.MetaDataObjects;

/// <summary>
/// Represents the metadata of an http message (either request or response)
/// </summary>
public record Http
{
    /// <summary>Optional outgoing HTTP method token (for example PATCH). Null uses the action method.</summary>
    public string? Method { get; init; }

    /// <summary>Per-message route appended to the configured base path. Uri takes precedence.</summary>
    public string? Route { get; init; }

    /// <summary>Unescaped query pairs appended to the selected URI. Existing keys are retained, including duplicates.</summary>
    public IDictionary<string, string>? QueryParameters { get; init; }

    /// <summary>Received response status; used as the response status by Mocker. Not a request option.</summary>
    public int? StatusCode { get; init; }

    /// <summary>Response reason phrase. This is not an outgoing request option.</summary>
    public string? ReasonPhrase { get; init; }

    /// <summary>Observed HTTP protocol version. Does not select an outgoing request or server response version.</summary>
    public string? Version { get; init; }

    /// <summary>Complete request destination override; relative values resolve against the configured base directory.
    /// Takes precedence over Route and path substitution, so captured request metadata can be replayed.</summary>
    public Uri? Uri { get; init; }

    /// <summary>
    /// Content headers. A non-null outgoing dictionary replaces configured content headers; an empty one clears them.
    /// </summary>
    public IDictionary<string, string>? Headers { get; init; }

    /// <summary>Request headers. Null inherits configured headers; a non-null dictionary replaces that category.
    /// Client-level authentication remains active unless explicitly overridden with Authorization.</summary>
    public IDictionary<string, string>? RequestHeaders { get; init; }

    /// <summary>Received response headers, or headers to emit from Mocker. Not applied to outgoing requests.</summary>
    public IDictionary<string, string>? ResponseHeaders { get; init; }

    /// <summary>Received response trailers. Not applied to outgoing requests.
    /// Mocker response emission requires a version with trailer support and a compatible server transport.</summary>
    public IDictionary<string, string>? TrailingHeaders { get; init; }

    /// <summary>
    /// Unescaped values for {name} placeholders in an outgoing route path. Each value is encoded as one segment.
    /// Missing values fail before sending; extra keys are allowed. Uri overrides bypass substitution.
    /// Mocker also populates this dictionary from matched incoming path segments.
    /// </summary>
    public IDictionary<string, string>? PathParameters { get; set; }
}
