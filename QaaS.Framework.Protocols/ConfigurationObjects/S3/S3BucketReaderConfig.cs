using System.ComponentModel;
using QaaS.Framework.Configurations.CommonConfigurationObjects;

namespace QaaS.Framework.Protocols.ConfigurationObjects.S3;

public record S3BucketReaderConfig : S3BucketConfig, IReaderConfig
{
    [Description("Prefix of the objects to read from s3 bucket"), DefaultValue("")]
    public string Prefix { get; set; } = "";

    [
        Description(
            "Optional S3 key delimiter used with `Prefix` to browse one hierarchy level. S3 returns objects whose remaining key "
                + $"after `{nameof(Prefix)}` does not contain the delimiter and rolls deeper keys into common prefixes, which are not consumed as objects. "
                + $"For example, `{nameof(Prefix)}: events/` with `Delimiter: /` reads objects directly under `events/`, but not objects under `events/archive/`. "
                + "Leave empty to read every object matching the prefix, including objects at deeper levels. A delimiter selects a hierarchy level; use `Prefix` to select a path."
        ),
        DefaultValue("")
    ]
    public string Delimiter { get; set; } = "";

    [Description(
        "The maximum number of times to retry when an action against the S3 fails due to maximum S3 supported"
            + " IOPS, if no value is given will retry indefinitely"
    )]
    public int? MaximumRetryCount { get; set; } // By default null which means no limit to the amounts of retries

    [
        Description(
            "Whether to skip the read of empty s3 objects or not, if true skips them if false doesnt skip them"
        ),
        DefaultValue(false)
    ]
    public bool SkipEmptyObjects { get; set; } = false;

    [
        Description(
            "Whether to only read messages that were last modified after the start of the read action"
                + " (true) or read all messages regardless of latest modification time (false)"
        ),
        DefaultValue(false)
    ]
    public bool ReadFromRunStartTime { get; set; } = false;

    [
        Description(
            "Whether to read S3 user-defined metadata for each consumed object into `MetaData.Storage.Headers`, where assertions can access it. "
                + "Enabling this performs one additional S3 metadata request per object; leave false to avoid those requests."
        ),
        DefaultValue(false)
    ]
    public bool ReadStorageHeaders { get; set; } = false;
}
