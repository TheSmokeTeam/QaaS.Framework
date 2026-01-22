using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Protocols.ConfigurationObjects.RabbitMq;

public record BaseRabbitMqConfig
{
    [Required, Description("Rabbitmq hostname")]
    public string? Host { get; set; }

    [Description("Rabbitmq username"), DefaultValue("admin")]
    public string Username { get; set; } = "admin";

    [Description("Rabbitmq password"), DefaultValue("admin")]
    public string Password { get; set; } = "admin";

    [Range(0, 65535), Description("Rabbitmq Amqp port"), DefaultValue(5672)]
    public int Port { get; set; } = 5672;

    [Description("Rabbitmq virual host to access during this connection"), DefaultValue("/")]
    public string VirtualHost { get; set; } = "/";
    
    [Range(0, int.MaxValue), Description(
         "Amount of time protocol operations (e.g. queue.declare) are allowed to take before timing out in seconds"),
     DefaultValue(5)]
    public int ContinuationTimeoutSeconds { get; set; } = 5;
    
    [Range(0, int.MaxValue), Description("Timeout setting for connection attempts in seconds"), DefaultValue(5)]
    public int RequestedConnectionTimeoutSeconds { get; set; } = 5;
    
    [Range(0, int.MaxValue), Description(
         "Amount of time protocol handshake operations are allowed to take before timing out in seconds"),
     DefaultValue(10)]
    public int HandshakeContinuationTimeoutSeconds { get; set; } = 10;
}