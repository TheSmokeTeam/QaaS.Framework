using System.ComponentModel;
using QaaS.Framework.Configurations.CommonConfigurationObjects;

namespace QaaS.Framework.Protocols.ConfigurationObjects.S3;

public record S3BucketReaderConfig: S3BucketConfig, IReaderConfig
{
    [Description("Prefix of the objects to read from s3 bucket"), DefaultValue("")]
    public string Prefix { get; set; } = "";
    
    [Description("Delimiter of the objects to read from s3 bucket, this determines what objects will be retrieved from the bucket, " +
                 $"objects that have at least one occurence of the delimiter in their relative path after the `{nameof(Prefix)}` " +
                 "will not be retrieved from the bucket."), DefaultValue("")]
    public string Delimiter { get; set; } = "";
    
    [Description("The maximum number of times to retry when an action against the S3 fails due to maximum S3 supported" +
                 " IOPS, if no value is given will retry indefinitely")]
    public int? MaximumRetryCount { get; set; } // By default null which means no limit to the amounts of retries

    [Description("Whether to skip the read of empty s3 objects or not, if true skips them if false doesnt skip them"),
     DefaultValue(false)]
    public bool SkipEmptyObjects { get; set; } = false;
    
    [Description("Whether to only read messages that were last modified after the start of the read action" +
                 " (true) or read all messages regardless of latest modification time (false)"), DefaultValue(false)]
    public bool ReadFromRunStartTime { get; set; } = false;
    
}