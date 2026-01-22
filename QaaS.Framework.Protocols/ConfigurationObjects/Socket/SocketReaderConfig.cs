using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Socket;

public record SocketReaderConfig : BaseSocketConfig, IReaderConfig
{
  
    [Range(0, int.MaxValue), DefaultValue(5000), Description("Timeout receiving a packet in milliseconds")]
    public int ReceiveTimeoutMs { get; set; } = 5000;

    [Range(0, int.MaxValue), DefaultValue(1024 * 2 * 2 * 2 * 2 * 2 * 2),
     Description("Size of the receive buffer, in bytes")]
    public int BufferSize { get; set; } = 1024 * 2 * 2 * 2 * 2 * 2 * 2;

    [DefaultValue(null), Description("Character to seperate read buffers into messages (delimiter) - when left" +
                                     " blank - messages will be set by default buffer seperation")]
    public char? SeperationChar { get; set; }

  
}