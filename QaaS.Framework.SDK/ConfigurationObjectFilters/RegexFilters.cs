using System.Text.RegularExpressions;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;

namespace QaaS.Framework.SDK.ConfigurationObjectFilters;

/// <summary>
/// Implements RegexFilter functions for relevant data sources.
/// </summary>
public static class RegexFilters
{
    /// <summary>
    /// RegexFilter for the DataSource objects using the DataSource's name.
    /// </summary>
    /// <param name="data"> the DataSource to filter </param>
    /// <param name="pattern"> the Regex pattern to filter by </param>
    /// <returns> True if passed the filter, False otherwise. </returns>
    public static bool DataSource(DataSource data, string pattern) => Regex.IsMatch(data.Name, pattern);
    
    /// <summary>
    /// RegexFilter for the SessionData objects using the SessionData's name.
    /// </summary>
    /// <param name="data"> the SessionData to filter </param>
    /// <param name="pattern"> the Regex pattern to filter by </param>
    /// <returns> True if passed the filter, False otherwise. </returns>
    public static bool SessionData(SessionData data, string pattern) => Regex.IsMatch(data.Name, pattern);
}