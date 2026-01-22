using QaaS.Framework.SDK.DataSourceObjects;

namespace QaaS.Framework.SDK.ExecutionObjects;

public interface IExecutionData
{
    public List<DataSource> DataSources { get; set; }
}