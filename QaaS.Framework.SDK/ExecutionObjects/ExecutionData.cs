using QaaS.Framework.SDK.AssertionObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;

namespace QaaS.Framework.SDK.ExecutionObjects;

public class ExecutionData : IExecutionData
{
    public List<DataSource> DataSources { get; set; } = new();
    public List<SessionData?> SessionDatas { get; set; } = new();
    public List<IAssertionResult> AssertionResults { get; set; } = new();
}