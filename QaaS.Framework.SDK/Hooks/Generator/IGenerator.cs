using System.Collections.Immutable;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;

namespace QaaS.Framework.SDK.Hooks.Generator;

/// <summary>
/// Represents a generator that may be used by QaaS to generate relevant test data
/// </summary>
public interface IGenerator : IHook
{
    /// <summary>
    /// Generates enumerable of data with the given configuration and relevant data sources
    /// </summary>
    /// <param name="sessionDataList"> The data of the sessions that occurred before this data source scope </param>
    /// <param name="dataSourceList"> The relevant data source for this data source scope </param>
    /// <returns> Enumerable of data </returns>
    public IEnumerable<Data<object>> Generate(IImmutableList<SessionData> sessionDataList, IImmutableList<DataSource> dataSourceList);
}