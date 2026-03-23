using System.ComponentModel;
using QaaS.Framework.Configurations.CustomValidationAttributes;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.ExecutionObjects;

namespace QaaS.Framework.Executions;

/// <summary>
/// Base class for building execution instances 
/// </summary>
public abstract class BaseExecutionBuilder<TContext, TExecutionData>
    where TContext : BaseContext<TExecutionData> where TExecutionData : class, IExecutionData, new()
{
    [AllItemsInEnumerablePropertyInEnumerableExistAsPropertyInEnumerable(
         nameof(DataSourceBuilder.DataSourceNames), nameof(DataSourceBuilder.Name)),
     UniquePropertyInEnumerable(nameof(DataSourceBuilder.Name)),
     Description("List of data sources that can be used in the rest of the execution." +
                 " They provide data that can be sent to the tested system or used by the execution itself to perform " +
                 "a multitude of logics.")]
    internal DataSourceBuilder[]? DataSources { get; set; } = [];

    protected abstract IEnumerable<DataSource> BuildDataSources();
    public abstract BaseExecution Build();

    protected TContext Context = null!;
}
