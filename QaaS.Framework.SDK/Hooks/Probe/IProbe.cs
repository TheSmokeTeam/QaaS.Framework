using System.Collections.Immutable;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;

namespace QaaS.Framework.SDK.Hooks.Probe;

/// <summary>
/// Represents a probe that may be used by QaaS to run specific function
/// </summary>
public interface IProbe : IHook
{

    /// <summary>
    /// Runs a user's custom Probe function
    /// </summary>
    /// <param name="sessionDataList"> The data of the sessions that occurred before this data source scope </param>
    /// <param name="dataSourceList"> The relevant data source for this data source scope </param>
    public void Run(IImmutableList<SessionData> sessionDataList, IImmutableList<DataSource> dataSourceList);
}