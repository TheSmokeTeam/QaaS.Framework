using System.ComponentModel;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Sql;

public record PostgreSqlReaderConfig : SqlReaderConfig
{
    [Description($"Whether the {nameof(InsertionTimeField)} is of type `timezonetz` (true) or not (false)," +
                 $" if it is and this is configurd to true it will be treated as if its timezone is utc"),
     DefaultValue(false)]
    public bool IsInsertionTimeFieldTimeZoneTz { get; set; } = false;

};