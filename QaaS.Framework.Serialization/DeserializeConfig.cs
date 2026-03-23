using System.ComponentModel;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Framework.Serialization;

public record DeserializeConfig
{
    [Description("The deserializer type to use for deserializing." +
                 " Null means no deserialization will happen." +
                 " Options are all available `QaaS.Framework.Serialization` deserializers"),
     DefaultValue(null)]
    public SerializationType? Deserializer { get; set; } = null;
    
    [RequiredIfAny(nameof(Deserializer), SerializationType.ProtobufMessage), 
     Description("Configuration for making deserializer deserialize into a specific C# object, " +
                 "if set to null will deserialize to default deserilizer's C# object"), DefaultValue(null)]
    internal SpecificTypeConfig? SpecificType { get; set; } = null;

    public SpecificTypeConfig? ReadSpecificType() => SpecificType;
}
