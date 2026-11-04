using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class MsSqlJsonIntegrationTests
{
    [TestCase(false)]
    [TestCase(true)]
    [SetCulture("de-DE")]
    public void ParsedScalars_RoundTripVarcharAndDecimal(bool highPrecision)
    {
        var connectionString = Environment.GetEnvironmentVariable("QAAS_MSSQL_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            Assert.Ignore("Set QAAS_MSSQL_CONNECTION_STRING to run SQL Server integration tests.");
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        var table = "qaas_json_" + Guid.NewGuid().ToString("N");
        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE TABLE {table} (label varchar(100), amount decimal(38,18))";
        command.ExecuteNonQuery();
        var protocol = new MsSqlProtocol(
            new MsSqlSenderConfig
            {
                ConnectionString = connectionString,
                TableName = table,
                ColumnTypes = highPrecision ? new() { ["amount"] = "decimal(38,18)" } : null,
            },
            Globals.Logger
        );
        try
        {
            protocol.Connect();
            protocol
                .SendChunk([
                    new Data<object>
                    {
                        Body = JsonNode.Parse(
                            highPrecision
                                ? """{"label":"O'Brien","amount":12345678901234567890.123456789012345678}"""
                                : """{"label":"O'Brien","amount":1.25}"""
                        ),
                    },
                ])
                .ToArray();
            command.CommandText = $"SELECT label FROM {table}";
            Assert.That(command.ExecuteScalar(), Is.EqualTo("O'Brien"));
            command.CommandText = $"SELECT CAST(amount AS varchar(50)) FROM {table}";
            Assert.That(
                command.ExecuteScalar(),
                Is.EqualTo(
                    highPrecision
                        ? "12345678901234567890.123456789012345678"
                        : "1.250000000000000000"
                )
            );
        }
        finally
        {
            try
            {
                protocol.Disconnect();
            }
            finally
            {
                command.CommandText = $"DROP TABLE {table}";
                command.ExecuteNonQuery();
            }
        }
    }

    [TestCase("parsed", "en-US", 1)]
    [TestCase("parsed", "de-DE", 2)]
    [TestCase("dictionary", "en-US", 2)]
    [TestCase("dictionary", "de-DE", 1)]
    [TestCase("poco", "en-US", 1)]
    [TestCase("poco", "de-DE", 2)]
    public void DefaultBulkSend_RoundTripsParsedAndSerializedBodies(
        string source,
        string culture,
        int chunkSize
    )
    {
        var connectionString = Environment.GetEnvironmentVariable("QAAS_MSSQL_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            Assert.Ignore("Set QAAS_MSSQL_CONNECTION_STRING to run SQL Server integration tests.");

        using var connection = new SqlConnection(connectionString);
        connection.Open();
        var table = "qaas_json_" + Guid.NewGuid().ToString("N");
        using var command = connection.CreateCommand();
        command.CommandText =
            $"CREATE TABLE {table} (id int, label varchar(100), amount decimal(20,4), active bit, note varchar(100) NULL, nested nvarchar(max))";
        command.ExecuteNonQuery();
        var previousCulture = CultureInfo.CurrentCulture;
        var protocol = new MsSqlProtocol(
            new MsSqlSenderConfig { ConnectionString = connectionString, TableName = table },
            Globals.Logger
        );
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            protocol.Connect();
            var rows = Enumerable
                .Range(0, 2)
                .Select(i =>
                {
                    object body = source switch
                    {
                        "parsed" => JsonNode.Parse(
                            i == 0
                                ? """{"id":0,"label":"O'Brien","amount":1.25,"active":true,"note":null,"nested":{"x":1}}"""
                                : """{"id":1,"label":"O'Brien","amount":7,"active":false,"note":"present","nested":{"x":1}}"""
                        )!,
                        "dictionary" => new Dictionary<string, object?>
                        {
                            ["id"] = i,
                            ["label"] = "O'Brien",
                            ["amount"] = i == 0 ? 1.25m : 7m,
                            ["active"] = i == 0,
                            ["note"] = i == 0 ? null : "present",
                            ["nested"] = new { x = 1 },
                        },
                        _ => new
                        {
                            id = i,
                            label = "O'Brien",
                            amount = i == 0 ? 1.25m : 7m,
                            active = i == 0,
                            note = i == 0 ? null : "present",
                            nested = new { x = 1 },
                        },
                    };
                    return new Data<object> { Body = body };
                })
                .ToArray();
            foreach (var chunk in rows.Chunk(chunkSize))
                Assert.That(protocol.SendChunk(chunk).Count(), Is.EqualTo(chunk.Length));

            command.CommandText =
                $"SELECT id, label, amount, active, note, nested FROM {table} ORDER BY id";
            using var reader = command.ExecuteReader();
            for (var i = 0; i < 2; i++)
            {
                Assert.That(reader.Read(), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(reader.GetInt32(0), Is.EqualTo(i));
                    Assert.That(reader.GetString(1), Is.EqualTo("O'Brien"));
                    Assert.That(reader.GetDecimal(2), Is.EqualTo(i == 0 ? 1.25m : 7m));
                    Assert.That(reader.GetBoolean(3), Is.EqualTo(i == 0));
                    Assert.That(
                        reader.GetValue(4),
                        Is.EqualTo(i == 0 ? (object)DBNull.Value : "present")
                    );
                    Assert.That(reader.GetString(5), Is.EqualTo("{\"x\":1}"));
                });
            }
            Assert.That(reader.Read(), Is.False);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            try
            {
                protocol.Disconnect();
            }
            finally
            {
                command.CommandText = $"DROP TABLE {table}";
                command.ExecuteNonQuery();
            }
        }
    }
}
