using System.Collections.Immutable;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Framework.SDK.Hooks.Processor;

/// <summary>
/// Represents a processor which processes transactions.
/// </summary>
public interface ITransactionProcessor : IProcessor
{
    /// <summary>
    /// Processes transaction request for response data.
    /// </summary>
    /// <param name="dataSourceList"> The relevant data source for this processor scope </param>
    /// <param name="requestData"> The relevant request input to process </param>
    /// <returns> response data </returns>
    public Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData);
}