using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Elastic;

public record ElasticReaderConfig : ElasticIndicesRegex, IReaderConfig
{
    [Description("The json path to the main timestamp field to use for deciding a queried item's latest update time"),
     DefaultValue("@timestamp")]
    public string TimestampField { get; set; } = "@timestamp";

    [Range(1, int.MaxValue), Description(
         "The size of the batch of documents to read from the elastic index pattern while scrolling its contents"),
     DefaultValue(1000)]
    public int ReadBatchSize { get; set; } = 1000;

    [Range(1, uint.MaxValue), Description(
         "Specify how long a consistent view of the index should be maintained for scrolled search in milliseconds"),
     DefaultValue(100000)]
    public uint ScrollContextExpirationMs { get; set; } = 100000;

    [Description("Whether to only read messages that arrived to the elastic after the start of the read action" +
                 " (true) or read all messages regardless of arrival time (false)"), DefaultValue(false)]
    public bool ReadFromRunStartTime { get; set; } = false;

    [Range(0, uint.MaxValue),
     Description($"If the {nameof(ReadFromRunStartTime)} is enabled, this property specifies how " +
                 $"far before the start of the read action to start consuming messages in seconds"), DefaultValue(0)]
    public uint FilterSecondsBeforeRunStartTime { get; set; } = 0;
}