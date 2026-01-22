using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Elastic;

/// <summary>
/// Base elastic indices configurations relevant to any action related to elasticsearch indices
/// </summary>
public record BaseElasticIndices : BaseElasticConfig
{
    [Required, Description("The index pattern of the relevant indices")]
    public string? IndexPattern { get; set; }
}