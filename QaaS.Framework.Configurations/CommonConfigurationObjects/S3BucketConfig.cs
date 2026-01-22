using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Configurations.CommonConfigurationObjects;

/// <summary>
/// S3 bucket configuration object
/// </summary>
public record S3BucketConfig
{
    [Required, Description("Name of S3 bucket")]
    public string? StorageBucket { get; set; }
    
    [Required, Url, Description("S3 service url. for example `REDA`")]
    public string? ServiceURL { get; set; }
    
    [Required, Description("S3 account access key")]
    public string? AccessKey { get; set; }
    
    [Required, Description("S3 account secret access key")]
    public string? SecretKey { get; set; }
    
    [Description("When true, requests will always use path style addressing"), DefaultValue(true)]
    public bool ForcePathStyle { get; set; } = true;
}