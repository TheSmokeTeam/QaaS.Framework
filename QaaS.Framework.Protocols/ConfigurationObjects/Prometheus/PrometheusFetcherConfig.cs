using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Prometheus;

public record PrometheusFetcherConfig : IFetcherConfig
{
    [Required, Url, Description("The prometheus' url, the base url without any route")]
    public string? Url { get; set; }

    [Required, Description("The expression to query the prometheus query_range API with to collect data")]
    public string? Expression { get; set; }

    [Range(1, uint.MaxValue),
     Description(
         "The interval to sample the expression's value from the prometheus during the collection time range in milliseconds"),
     DefaultValue(30_000)]
    public uint SampleIntervalMs { get; set; } = 30_000;

    [Range(1, uint.MaxValue),
     Description("The timeout in milliseconds for the execution of the query sent to the prometheus API"),
     DefaultValue(120_000)]
    public uint TimeoutMs { get; set; } = 120_000;

    [Description("The api key for interacting with prometheus")]
    public string? ApiKey { get; set; }
}