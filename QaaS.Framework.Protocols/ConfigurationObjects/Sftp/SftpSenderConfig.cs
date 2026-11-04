using System.ComponentModel;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Sftp;

using System.ComponentModel.DataAnnotations;

public record SftpSenderConfig : BaseSftpConfig, IObjectNamingGeneratorConfig, ISenderConfig
{
    [
        Description(
            "Optional SSH.NET upload buffer size in bytes. Omitted preserves the client default. Tune against file size, server limits and memory."
        ),
        Range(1, int.MaxValue)
    ]
    public uint? UploadBufferSize { get; set; }

    [Description("The object's naming prefix"), DefaultValue("")]
    public string Prefix { get; set; } = "";

    [
        Description("The naming type of the object naming generator"),
        DefaultValue(ObjectNamingGeneratorType.GrowingNumericalSeries)
    ]
    public ObjectNamingGeneratorType NamingType { get; set; } =
        ObjectNamingGeneratorType.GrowingNumericalSeries;
}
