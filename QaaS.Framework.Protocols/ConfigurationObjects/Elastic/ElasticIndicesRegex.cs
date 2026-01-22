using System.ComponentModel;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Elastic;

/// <summary>
/// Configuration for elastic indices including math pattern to filter out irrelevant documents
/// </summary>
public record ElasticIndicesRegex : BaseElasticIndices
{
    [Description("The match query string for the documents from the relevant indices"), DefaultValue("*")]
    public string MatchQueryString { get; set; } = "*";
}