using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Http;

public class HttpTransactorConfig : ITransactorConfig
{
    [Required, Description("The http method the transactor should perform")]
    public HttpMethods Method { get; set; }
    
    [Required, Url, Description(
         "The http server's address (needs to be with the protocol specification prefix http:// or https://)")]
    public string? BaseAddress { get; set; }

    [Range(0, 65535), Description("The port to send the requests to in the http server"), DefaultValue(8080)]
    public int? Port { get; set; } = 8080;

    [Description("The route in the http server to send the request to"), DefaultValue("")]
    public string Route { get; set; } = "";

    [Description("Default content headers to add to the http requests, " +
                 "will be overriden if sent data has http content headers in its metadata")]
    public Dictionary<string, string>? Headers { get; set; }

    [Description("Default request headers to add to the http requests, " +
                 "will be overriden if sent data has http request headers in its metadata")]
    public Dictionary<string, string>? RequestHeaders { get; set; }

    [Description("The JWT configurations for the generation and addition of a JWT as a Bearer authorization header, " +
                 "if this field is not configured will not use JwtAuth"), DefaultValue(null)]
    public JwtAuthConfig? JwtAuth { get; set; } = null;

    [Description("The amount of times to retry each failed request"), DefaultValue(1), Range(1, int.MaxValue)]
    public int Retries { get; set; } = 1;

    [Description("Time interval in milliseconds to wait between each retry of http request."),
     Range(0, int.MaxValue), DefaultValue(1000)]
    public int MessageSendRetriesIntervalMs { get; set; } = 1000;
}