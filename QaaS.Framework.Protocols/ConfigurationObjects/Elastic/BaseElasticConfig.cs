using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Elastic;

public record BaseElasticConfig
{
    [Required, Url, Description("The url of the elasticsearch")]
    public string? Url { get; set; }
    
    [Required, Description("The username of the elasticsearch")]
    public string? Username { get; set; }
    
    [Required, Description("The password of the elasticsearch")]
    public string? Password { get; set; }

    [Range(uint.MinValue, uint.MaxValue),
     Description("The timeout in milliseconds on the requests sent to the elastic"), DefaultValue(30000)]
    public uint RequestTimeoutMs { get; set; } = 30000;
}