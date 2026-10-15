using System.Data;
using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Moq;
using Npgsql;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class SqlColumnTypesTests
{
    private sealed class RowProtocol(SqlReaderConfig config, IDbConnection connection)
        : MockSqlProtocol(config, Globals.Logger, connection)
    {
        public void SendRow(Data<object> data) =>
            RowInsertIntoTable(GetDataTableFromRawDataChunk([data]));

        protected override string GetTimeFieldSqlFormat(DateTime time) =>
            $"FORMAT('{time:yyyy-MM-dd HH:mm:ss}')";
    }

    private sealed class TestPostgreSqlProtocol(PostgreSqlSenderConfig config, IDbCommand command)
        : PostgreSqlProtocol(config, Globals.Logger)
    {
        public bool UsedCopy { get; private set; }

        protected override IDbCommand CreateDbCommand() => command;

        protected override void InsertChunkToTable(DataTable chunkData) => UsedCopy = true;
    }

    private static string Insert(object? value, Dictionary<string, string>? types)
    {
        var command = new Mock<IDbCommand>();
        command.SetupAllProperties();
        var connection = new Mock<IDbConnection>();
        connection.Setup(c => c.CreateCommand()).Returns(command.Object);
        var protocol = new RowProtocol(
            new PostgreSqlReaderConfig { TableName = "measurements", ColumnTypes = types },
            connection.Object
        );
        protocol.SendRow(new Data<object> { Body = new { value } });
        return command.Object.CommandText;
    }

    [TestCase("double precision", "1.12")]
    [TestCase("numeric(10, 2)", "1.12")]
    [TestCase("text", "1.12")]
    [TestCase("timestamp", "2026-10-15 12:34:56.123456")]
    [TestCase("timestamp with time zone", "2026-10-15T12:34:56.123456+03:00")]
    [TestCase("boolean", "true")]
    [TestCase("uuid", "b5ca8237-a14f-4f65-965d-9bd9634566b8")]
    public void ExplicitType_BypassesInference(string type, string value)
    {
        Assert.That(
            Insert(value, new() { ["value"] = type }),
            Is.EqualTo($"INSERT INTO measurements (value) VALUES (CAST('{value}' AS {type}))")
        );
    }

    [Test]
    public void NumericJsonValue_UsesExplicitType()
    {
        Assert.That(
            Insert(1.12, new() { ["value"] = "double precision" }),
            Does.Contain("CAST('1.12' AS double precision)")
        );
    }

    [Test]
    public void ExplicitText_EscapesQuotes_AndDoesNotExecuteUdtExpressions()
    {
        Assert.That(
            Insert("Point('x')", new() { ["value"] = "text" }),
            Does.Contain("CAST('Point(''x'')' AS text)")
        );
    }

    [Test]
    public void ExplicitType_PreservesNull()
    {
        Assert.That(
            Insert(null, new() { ["value"] = "double precision" }),
            Is.EqualTo("INSERT INTO measurements (value) VALUES (NULL)")
        );
    }

    [TestCase(null)]
    [TestCase("unrelated")]
    public void UnspecifiedColumn_KeepsDateInference(string? otherColumn)
    {
        var types =
            otherColumn == null ? null : new Dictionary<string, string> { [otherColumn] = "text" };
        Assert.That(
            Insert("2026-10-15 12:34:56", types),
            Does.Contain("FORMAT('2026-10-15 12:34:56')")
        );
    }

    [Test]
    public void UnspecifiedType_KeepsUdtInference()
    {
        Assert.That(Insert("Point(1 2)", null), Does.Contain("VALUES (Point(1 2))"));
    }

    [Test]
    public void ExplicitNumericType_IsCultureIndependent()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.That(
                Insert(1.12, new() { ["value"] = "double precision" }),
                Does.Contain("CAST('1.12' AS double precision)")
            );
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Test]
    public void EmptyExplicitType_FailsClearly()
    {
        Assert.Throws<ArgumentException>(() => Insert(1.12, new() { ["value"] = " " }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PostgreSql_SingleAndChunkSend_ApplyTypesAndKeepUnspecifiedColumns(bool chunk)
    {
        var command = new Mock<IDbCommand>();
        command.SetupAllProperties();
        using var parameterOwner = new NpgsqlCommand();
        command.SetupGet(c => c.Parameters).Returns(parameterOwner.Parameters);
        command.Setup(c => c.CreateParameter()).Returns(() => new NpgsqlParameter());
        var config = new PostgreSqlSenderConfig
        {
            TableName = "measurements",
            ColumnTypes = new() { ["price"] = "double precision", ["label"] = "text" },
        };
        var protocol = new TestPostgreSqlProtocol(config, command.Object);
        var data = new Data<object>
        {
            Body = JsonNode.Parse(
                """{"price":1.12,"label":"1.12","created_at":"2026-10-15 12:34:56"}"""
            ),
        };

        if (chunk)
            protocol.SendChunk([data, data]).ToList();
        else
            protocol.Send(data);

        Assert.Multiple(() =>
        {
            Assert.That(
                command.Object.CommandText,
                Does.Contain(
                    chunk ? "CAST(@p0 AS double precision)" : "CAST('1.12' AS double precision)"
                )
            );
            Assert.That(
                command.Object.CommandText,
                Does.Contain(chunk ? "CAST(@p1 AS text)" : "CAST('1.12' AS text)")
            );
            if (chunk)
                Assert.That(parameterOwner.Parameters[2].Value, Is.EqualTo("2026-10-15 12:34:56"));
            else
                Assert.That(command.Object.CommandText, Does.Contain("TO_TIMESTAMP("));
            Assert.That(protocol.UsedCopy, Is.False);
            command.Verify(c => c.ExecuteNonQuery(), Times.Exactly(chunk ? 2 : 1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PostgreSql_WithoutTypes_KeepsBulkCopy(bool emptyMapping)
    {
        var command = new Mock<IDbCommand>();
        var protocol = new TestPostgreSqlProtocol(
            new PostgreSqlSenderConfig { ColumnTypes = emptyMapping ? new() : null },
            command.Object
        );
        protocol.SendChunk([new Data<object> { Body = new { price = 1.12 } }]).ToList();
        Assert.That(protocol.UsedCopy, Is.True);
        command.Verify(c => c.ExecuteNonQuery(), Times.Never);
    }

    [Test]
    public void ColumnTypes_BindsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ColumnTypes:price"] = "double precision",
                    ["ColumnTypes:created_at"] = "timestamp with time zone",
                }
            )
            .Build();
        var config = configuration.Get<PostgreSqlSenderConfig>()!;
        Assert.That(
            config.ColumnTypes,
            Is.EquivalentTo(
                new Dictionary<string, string>
                {
                    ["price"] = "double precision",
                    ["created_at"] = "timestamp with time zone",
                }
            )
        );
    }
}
