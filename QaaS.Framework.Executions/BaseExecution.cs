using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.ExecutionObjects;

namespace QaaS.Framework.Executions;

public abstract class BaseExecution : IDisposable
{
    protected Context Context { get; set; } = null!;

    protected ExecutionType Type { get; set; }

    public abstract int Start();

    public abstract void Dispose();
}