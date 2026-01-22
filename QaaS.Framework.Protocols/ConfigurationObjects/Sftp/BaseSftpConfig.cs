using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Sftp;

public record BaseSftpConfig()
{
    [Required, Description("The hostname of the remote machine")]
    public string Hostname { get; set; }

    [Required, Description("The username for accessing the remote machine")]
    public string Username { get; set; }

    [Required, Description("The password for accessing the remote machine")]
    public string Password { get; set; }

    [Description("The port in the remote machine"), DefaultValue(22), Range(0, 65535)]
    public int Port { get; set; } = 22;

    [Required, Description("The path of the relevant directory in the remote machine")]
    public string Path { get; set; }
}
