using System.ComponentModel;

namespace QaaS.Framework.Serialization;

public record SerializeConfig
{
    [Description("The serializer type to use for serializing." +
                 " Null means no serialization will happen." +
                 " Options are all available `QaaS.Framework.Serialization` serializers"),
     DefaultValue(null)]
    public SerializationType? Serializer { get; set; } = null;
}