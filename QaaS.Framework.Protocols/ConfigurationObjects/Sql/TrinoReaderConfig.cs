using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Sql;

public record TrinoReaderConfig : SqlReaderConfig
{
    [
        Description(
            "Optional Trino client result buffer budget in bytes. When omitted, the provider uses a 50 MiB buffer. This is independent of Runner result retention."
        ),
        Range(typeof(long), "1", "9223372036854775807")
    ]
    public long? ReadBufferSizeBytes { get; set; }

    [Required, Description("The username to login to the Trino")]
    public string? Username { get; set; }

    [Required, Description("The password to login to the Trino")]
    public string? Password { get; set; }

    [Required, Description("The client tag in the Trino, team or environment tag")]
    public string? ClientTag { get; set; }

    [Required, Description("The name of the schema that holds the table name in it")]
    public string? Schema { get; set; }

    [Required, Description("The catalog that the table name in it")]
    public string? Catalog { get; set; }

    [Required, Description("The hostname of the Trino")]
    public string? Hostname { get; set; }
};
