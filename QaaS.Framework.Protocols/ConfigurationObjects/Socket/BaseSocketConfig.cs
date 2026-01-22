using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Socket;

public record BaseSocketConfig
{
    [Required, Description("Socket connection endpoint hostname")]
    public string? Host { get; set; }

    [Required, Range(0, 65535), Description("Socket connection endpoint port")]
    public int? Port { get; set; }

    [Required, Description("Specifies the protocol to use in the socket")]
    public ProtocolType? ProtocolType { get; set; }
    
    [Description("Specifies the type of socket"), DefaultValue(SocketType.Stream)]
    public SocketType SocketType { get; set; } = SocketType.Stream;
    
    [Description("Specifies the addressing scheme to use in the socket"), DefaultValue(AddressFamily.InterNetwork)]
    public AddressFamily AddressFamily { get; set; } = AddressFamily.InterNetwork;

}