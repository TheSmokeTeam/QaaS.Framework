using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Sql;

public record SqlReaderConfig : SqlConfig, IReaderConfig
{
    [RequiredIfAny(nameof(ReadFromRunStartTime), true),
     Description(
         "The insertion time field name, in cases where the table can be updated this will be the update time field")]
    public string? InsertionTimeField { get; set; }

    [Range(-12, 14), Description(
         "The time zone hour difference in comparison to UTC at summer time (daylight saving time) of the insertion time field")
     , DefaultValue(0)]
    public int InsertionTimeTimeZoneOffsetSummerTime { get; set; } = 0;

    [Description(
        "The where statement (without the where keyword) to add to the sql query to filter db query results, " +
        "if no statement is given doesn't use where in the query")]
    public string? WhereStatement { get; set; } = null;

    [Description("Whether to only read messages that arrived to the database after the start of the read action" +
                 " (true) or read all messages regardless of arrival time (false)"), DefaultValue(false)]
    public bool ReadFromRunStartTime { get; set; } = false;

    [Range(0, uint.MaxValue),
     Description($"If the {nameof(ReadFromRunStartTime)} is enabled, this property specifies how " +
                 $"far before the start of the read action to start reading messages in seconds"), DefaultValue(0)]
    public uint FilterSecondsBeforeRunStartTime { get; set; } = 0;

    [Description("The columns to ignore in the sql query results, if no columns are given doesn't ignore any columns"),
     DefaultValue(new string[] { })]
    public string[] ColumnsToIgnore { get; set; } = [];
};