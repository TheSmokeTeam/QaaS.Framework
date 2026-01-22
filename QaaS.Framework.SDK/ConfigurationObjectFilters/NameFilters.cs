using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;

namespace QaaS.Framework.SDK.ConfigurationObjectFilters;

/// <summary>
/// Implements NameFilter functions for relevant data sources.
/// </summary>
public static class NameFilters
{
    /// <summary>
    /// RegexFilter for the DataSource objects using the DataSource's name.
    /// </summary>
    /// <param name="data"> the DataSource to filter </param>
    /// <param name="filterName"> the name to filter by </param>
    /// <returns> True if passed the filter, False otherwise. </returns>
    public static bool DataSource(DataSource data, string filterName) => data.Name == filterName;
    
    /// <summary>
    /// RegexFilter for the SessionData objects using the SessionData's name.
    /// </summary>
    /// <param name="data"> the SessionData to filter </param>
    /// <param name="filterName"> the name to filter by </param>
    /// <returns> True if passed the filter, False otherwise. </returns>
    public static bool SessionData(SessionData data, string filterName) => data.Name == filterName;
}