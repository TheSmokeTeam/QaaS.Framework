using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Elastic;

public record ElasticSenderConfig : BaseElasticConfig, ISenderConfig
{
    [Required, Description("Name of the index to publish documents to. if the index doesn't exist," +
                           " it will create the index")]
    public string? IndexName { get; set; }
    
    [Description("Batch size of publishing actions to the elastic index, when configured to null all generation data is" +
                 " considered as one batch"), DefaultValue(null), Range(1, int.MaxValue)]
    public int? BatchSize { get; set; } = null;
    
    [Description("Whether to publish to elastic asynchronously (faster but dosen't publish by the given order)"),
     DefaultValue(false)]
    public bool PublishAsync { get; set; } = false;

};