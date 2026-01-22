using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Sql;

public record SqlConfig
{
    [Required, Description("The connection string to the database")]
    public string? ConnectionString { get; set; }

    [Required, Description("The table to insert data to")]
    public string? TableName { get; set; }

    [Range(1, int.MaxValue), Description(
         "The wait time (in seconds) before terminating the attempt to execute an sql copy/insertion command" +
         " and generating an error"),
     DefaultValue(30)]
    public int CommandTimeoutSeconds { get; set; } = 30;
};