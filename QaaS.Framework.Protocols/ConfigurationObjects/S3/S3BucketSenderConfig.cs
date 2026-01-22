using System.ComponentModel;
using QaaS.Framework.Configurations.CommonConfigurationObjects;

namespace QaaS.Framework.Protocols.ConfigurationObjects.S3;

public record S3BucketSenderConfig : S3BucketConfig, ISenderConfig
{
    [Description("The object's naming prefix"), DefaultValue("")]
    public string Prefix { get; set; } = "";

    [Description("The naming type of the object naming generator"),
     DefaultValue(ObjectNamingGeneratorType.GrowingNumericalSeries)]
    public ObjectNamingGeneratorType S3SentObjectsNaming { get; set; } = ObjectNamingGeneratorType.GrowingNumericalSeries;

    [Description("The number of times to retry sending to S3 in case the s3 maximum IO is reached"),
     DefaultValue(null)]
    public int? Retries { get; set; } = null;
    
    [Description($"S3 Storage Class Definitions. Options:" +
                 $"[`{nameof(S3StorageClassEnum.Glacier)}` - The GLACIER storage is for object that are stored in Amazon Glacier. This storage class is for objects that are for archival purpose and get operations are rare.  Durability 99.999999999 /" +
                 $" `{nameof(S3StorageClassEnum.Outposts)}` - The OUTPOSTS storage class for objects stored in a S3 Outpost /" +
                 $" `{nameof(S3StorageClassEnum.Standard)}` - The STANDARD storage class, which is the default storage class for S3.  Durability 99.999999999%; Availability 99.99% over a given year/" +
                 $" `{nameof(S3StorageClassEnum.DeepArchive)}` - S3 Glacier Deep Archive provides secure, durable object storage class for long term data archival. It is the ideal storage class to make an archival, durable copy of data that rarely, if ever, needs to be accessed. It can be used as an offline backup for their most important data assets and to meet long-term retention needs. /" +
                 $" `{nameof(S3StorageClassEnum.IntelligentTiering)} - IntelligentTiering makes it easy to lower your overall cost of storage by automatically placing data in the storage class that best matches the access patterns for the storage. With IntelligentTiering, you don’t need to define and manage individual policies for lifecycle data management or write code to transition objects between storage classes. Instead, you can use IntelligentTiering to manage transitions between Standard and S-IA without writing any application code. IntelligentTiering also manages transitions automatically to Glacier for long term archive in addition to S3 storage classes.` /" +
                 $" `{nameof(S3StorageClassEnum.ReducedRedundancy)}` - REDUCED_REDUNDANCY provides the same availability as standard, but at a lower durability.  Durability 99.99%; Availability 99.99% over a given year. /" +
                 $" `{nameof(S3StorageClassEnum.GlacierInstantRetrieval)}` - Constant GLACIER_IR for ObjectStorageClass /" +
                 $" `{nameof(S3StorageClassEnum.StandardInfrequentAccess)}` - The STANDARD_IA storage is for infrequently accessed objects. This storage class is for objects that are long-lived and less frequently accessed, like backups and older data.  Durability 99.999999999%; Availability 99.9% over a given year. /" +
                 $" `{nameof(S3StorageClassEnum.OneZoneInfrequentAccess)}` - The ONEZONE_IA storage is for infrequently accessed objects. It is similiar to STANDARD_IA, but only stores object data within one Availablity Zone in a given region.  Durability 99.999999999%; Availability 99% over a given year.]"),
     DefaultValue(S3StorageClassEnum.StandardInfrequentAccess)]
    public S3StorageClassEnum S3StorageClass { get; set; } = S3StorageClassEnum.StandardInfrequentAccess;
    
}