using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Socket;

public record SocketSenderConfig : BaseSocketConfig, ISenderConfig
{
    [Range(0, int.MaxValue), DefaultValue(5000), Description("socket's timeout sending a packet in milliseconds")]
    public int SendTimeoutMs { get; set; } = 5000;

    [Range(0, int.MaxValue), DefaultValue(65536),
     Description("The size of the send buffer, in bytes. " +
                  "Increasing it can improve sending speed substantially " +
                  "but will use more memory." +
                  "To achieve max speed the buffer needs to be the size of all sent data combined")]
    public int BufferSize { get; set; } = 65536;
    
    [NagleAlgorithmIsFalseWhenProtocolTypeIsNotTcp, Description("Whether to use the Nagle Algorithm (true) or not(false)." +
                 " The Nagle algorithm is a method used in TCP/IP networks to improve the efficiency of data transmission." +
                 " It's designed to reduce the number of small packets that are sent over the network." +
                 "The Nagle algorithm is designed to improve the efficiency of small packets," +
                 " but it can sometimes cause delays in the transmission of large packets."), DefaultValue(false)]
    public bool NagleAlgorithm { get; set; } = false;

    [Range(1, int.MaxValue), Description("the number of seconds to remain connected after sending all the data," +
                                          " null means it does not remain connected after sending the data."),
     DefaultValue(null)]
    public int? LingerTimeSeconds { get; set; } = null;
    
}

internal class NagleAlgorithmIsFalseWhenProtocolTypeIsNotTcp: ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var config = (SocketSenderConfig)validationContext.ObjectInstance;
        return config.NagleAlgorithm && config.ProtocolType != ProtocolType.Tcp
            ? new ValidationResult($"{nameof(config.NagleAlgorithm)} can only be enabled when" +
                                        $" {nameof(config.ProtocolType)} is {ProtocolType.Tcp.ToString()}")
            : ValidationResult.Success;
    }
}