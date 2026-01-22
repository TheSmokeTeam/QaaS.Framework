using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Protocols.ConfigurationObjects.IbmMq;

public record IbmMqReaderConfig : IReaderConfig
{
    [Required, Description("The name of the host machine hosting the IbmMq")]
    public string? HostName { get; set; }
    
    [Required, Description("The port number the IbmMq is listening on")]
    public int? Port { get; set; }
    
    [Required, Description("The name of the channel to connect to")]
    public string? Channel { get; set; }
    
    [Required, Description("The name of the IbmMq manager to connect to")]
    public string? Manager { get; set; }
    
    [Required, Description("Name of the queue to read messages from")]
    public string? QueueName { get; init; }
}