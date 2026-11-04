using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Protocols.ConfigurationObjects.IbmMq;

public record IbmMqReaderConfig : IReaderConfig
{
    [Description(
        "Optional MQCSP user name. When omitted, the channel determines the connection identity. Supply secrets through configuration overrides rather than committing them."
    )]
    public string? Username { get; set; }

    [Description(
        "Optional MQCSP password. Configure transport encryption on the MQ channel when sending credentials."
    )]
    public string? Password { get; set; }

    [Description(
        "Optional IBM MQ TLS CipherSpec. When omitted, no CipherSpec is configured; choose a value supported by the MQ client, operating system and channel."
    )]
    public string? SslCipherSpec { get; set; }

    [Description(
        "Optional IBM MQ certificate store/key repository, such as *USER or *SYSTEM on Windows. Store setup and supported paths depend on the client platform."
    )]
    public string? SslKeyRepository { get; set; }

    [Description("Optional distinguished-name pattern expected in the MQ server certificate.")]
    public string? SslPeerName { get; set; }

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
