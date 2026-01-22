using System.Collections.Immutable;
using QaaS.Framework.SDK.ConfigurationObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;

namespace QaaS.Framework.SDK.Hooks.BaseHooks;

/// <summary>
/// Skim Transaction Processor which returns empty-bodied data objects with Status Code configuration.
/// </summary>
public class StatusCodeTransactionProcessor : BaseTransactionProcessor<StatusCodeConfiguration>
{
    /// <inheritdoc />
    public override Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData) 
        => new () { MetaData = new MetaData { Http = new Http { StatusCode = Configuration.StatusCode }}};
}