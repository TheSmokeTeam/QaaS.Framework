using System.ComponentModel;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Sql;

public record SqlUdtSenderConfig : SqlConfig
{
    [DefaultValue(false),
     Description("Determines whether User-Defined Type (UDT) insertion is required." +
                 " If UDT insertion is not necessary, it is recommended to set this property to false." +
                 " UDT insertion can slow down data insertion, especially for large datasets." +
                 " Therefore, it is recommended to only use UDT insertion when necessary.")]
    public bool IsUDTInsertion { get; set; } = false;
};