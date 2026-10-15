using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Sql;

public record SqlConfig
{
    /// <summary>
    /// Optional SQL type expressions keyed by column name. Explicit types bypass automatic
    /// date and UDT inference during insertion. Omitted columns retain their existing behavior.
    /// </summary>
    [Description(
        "Optional column-name to SQL-type mapping (for example price: double precision, "
            + "created_at: timestamp). Uses SQL CAST instead of automatic inference. "
            + "Type expressions are trusted SQL configuration in the target database's dialect. "
            + "When configured, chunk sends use row inserts to apply the casts."
    )]
    public Dictionary<string, string>? ColumnTypes { get; set; }

    [Required, Description("The connection string to the database")]
    public string? ConnectionString { get; set; }

    [Required, Description("The table to insert data to")]
    public string? TableName { get; set; }

    [
        Range(1, int.MaxValue),
        Description(
            "The wait time (in seconds) before terminating the attempt to execute an sql copy/insertion command"
                + " and generating an error"
        ),
        DefaultValue(30)
    ]
    public int CommandTimeoutSeconds { get; set; } = 30;
};
