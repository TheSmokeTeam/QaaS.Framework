using System.ComponentModel;

namespace QaaS.Framework.SDK.Session;

public record DataFilter
{
    [Description("Whether to keep the `Body` in the data (true) or filter it (false)"), DefaultValue(true)]
    public bool Body { get; set; } = true;
    
    [Description("Whether to keep the `Timestamp` in the data (true) or filter it (false)"), DefaultValue(true)]
    public bool Timestamp { get; set; } = true;
    
    [Description("Whether to keep the `MetaData` in the data (true) or filter it (false)"), DefaultValue(true)]
    public bool MetaData { get; set; } = true;
}