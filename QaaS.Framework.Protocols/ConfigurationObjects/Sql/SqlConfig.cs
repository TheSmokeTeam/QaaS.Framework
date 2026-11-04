using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Sql;

public record SqlConfig
{
    [
        Description(
            "Bind every value in singular row INSERTs as data. When false, columns without an explicit ColumnTypes entry use date/UDT expression inference. Chunk sends use native bulk operations when ColumnTypes is empty and typed row transactions when ColumnTypes has entries."
        ),
        DefaultValue(false)
    ]
    public bool ParameterizeRows { get; set; }

    /// <summary>
    /// Optional SQL type expressions keyed by column name. Explicit types bypass automatic
    /// date and UDT inference during insertion. In singular sends, unmapped columns use inference
    /// unless ParameterizeRows is true. Typed chunk sends bind all values as parameters.
    /// </summary>
    [Description(
        "Optional column-name to SQL-type mapping (for example price: double precision, "
            + "created_at: timestamp). Uses SQL CAST or provider LOB binding for Oracle CLOB/NCLOB/BLOB. "
            + "Type expressions are trusted SQL configuration in the target database's dialect. "
            + "When nonempty, chunk sends use row inserts to apply the casts."
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
