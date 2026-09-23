using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Http;

public class HttpTransactorConfig : ITransactorConfig
{
    [Required, Description("Default HTTP method; per-item Http.Method overrides it")]
    public HttpMethods Method { get; set; }

    [
        Required,
        Url,
        Description(
            "The http server's address (needs to be with the protocol specification prefix http:// or https://)"
        )
    ]
    public string? BaseAddress { get; set; }

    [
        Range(0, 65535),
        Description("Port override applied to BaseAddress authority; null preserves its port"),
        DefaultValue(8080)
    ]
    public int? Port { get; set; } = 8080;

    [
        Description(
            "Default route appended to the base path. Supports {name} placeholders from per-item Http.PathParameters. Per-item Http.Route overrides this route; Http.Uri overrides the destination."
        ),
        DefaultValue("")
    ]
    public string Route { get; set; } = "";

    [Description(
        "Default content headers to add to the http requests, "
            + "replaced as a whole when item Http.Headers is non-null (empty clears defaults)"
    )]
    public Dictionary<string, string>? Headers { get; set; }

    [Description(
        "Default request headers to add to the http requests, "
            + "replaced as a whole when item Http.RequestHeaders is non-null (empty clears defaults)"
    )]
    public Dictionary<string, string>? RequestHeaders { get; set; }

    [
        Description(
            "The JWT configurations for the generation and addition of a JWT as a Bearer authorization header, "
                + "if this field is not configured will not use JwtAuth"
        ),
        DefaultValue(null)
    ]
    public JwtAuthConfig? JwtAuth { get; internal set; } = null;

    public JwtAuthConfig? ReadJwtAuth() => JwtAuth;

    [
        Description(
            "Maximum number of attempts including the initial request; only transport failures and timeouts are retried"
        ),
        DefaultValue(1),
        Range(1, int.MaxValue)
    ]
    public int Retries { get; set; } = 1;

    [
        Description("Time interval in milliseconds to wait between each retry of http request."),
        Range(0, int.MaxValue),
        DefaultValue(1000)
    ]
    public int MessageSendRetriesIntervalMs { get; set; } = 1000;
}
