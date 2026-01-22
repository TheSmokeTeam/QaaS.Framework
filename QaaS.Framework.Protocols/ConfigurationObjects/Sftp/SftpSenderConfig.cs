using System.ComponentModel;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Sftp;

public record SftpSenderConfig : BaseSftpConfig, IObjectNamingGeneratorConfig, ISenderConfig
{
    [Description("The object's naming prefix"), DefaultValue("")]
    public string Prefix { get; set; } = "";

    [Description("The naming type of the object naming generator"),
     DefaultValue(ObjectNamingGeneratorType.GrowingNumericalSeries)]
    public ObjectNamingGeneratorType NamingType { get; set; } = ObjectNamingGeneratorType.GrowingNumericalSeries;
    
}