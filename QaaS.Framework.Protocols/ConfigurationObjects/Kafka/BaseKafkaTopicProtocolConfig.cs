using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Kafka;

public record BaseKafkaTopicProtocolConfig
{
    [Required,
     Description("List of the kafka hostnames (each hostname should contain the port too for example: - 'host1:8080'")]
    public string[]? HostNames { get; set; }
    
    [Required, Description("Kafka Service with read permissions for the topic's username")]
    public string? Username { get; set; }

    [Required, Description("Kafka Service with read permissions for the topic's password")]
    public string? Password { get; set; }
    
    [Description("The Sasl's security protocol"), DefaultValue(SecurityProtocol.SaslPlaintext)]
    public SecurityProtocol SecurityProtocol { get; set; } = SecurityProtocol.SaslPlaintext;
    
    [Description("The Sasl mechanism used in the kafka"), DefaultValue(SaslMechanism.ScramSha256)]
    public SaslMechanism SaslMechanism { get; set; } = SaslMechanism.ScramSha256;
}