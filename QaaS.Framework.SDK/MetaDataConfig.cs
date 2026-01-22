using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.SDK;

public record MetaDataConfig
{
    [Required, Description("The team responsible for the tests")]
    public string? Team { get; set; }
    
    [Required, Description("The tested system name")]
    public string? System { get; set; }

    [Description("Extra labels added by the user"), DefaultValue(null)]
    public Dictionary<string, object> ExtraLabels { get; set; } = new();
}