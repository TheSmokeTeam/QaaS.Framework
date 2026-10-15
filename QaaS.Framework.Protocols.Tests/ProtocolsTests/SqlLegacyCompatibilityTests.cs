using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Moq;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class SqlLegacyCompatibilityTests
{
    private sealed class Protocol(SqlReaderConfig config, IDbConnection connection)
        : MockSqlProtocol(config, Globals.Logger, connection)
    {
        public DataTable? BulkData { get; private set; }

        public void InsertRows(DataTable rows) => RowInsertIntoTable(rows);

        protected override void InsertChunkToTable(DataTable rows) => BulkData = rows;

        // Before ColumnTypes, row insertion called DbConnection directly, even when this
        // reader-oriented factory was overridden. Preserve that extension-point contract too.
        protected override IDbCommand CreateDbCommand() =>
            throw new AssertionException(
                "Legacy row sends must use the connection's command factory."
            );

        protected override string GetTimeFieldSqlFormat(DateTime value) =>
            $"FORMAT('{value:yyyy-MM-dd HH:mm:ss}')";
    }

    private static readonly object?[] LegacyValues =
    [
        null,
        DBNull.Value,
        "",
        "plain text",
        "O'Brien",
        "a\\b;c\nnext",
        "NULL",
        "1.12",
        1.12,
        42,
        -7,
        12.34m,
        true,
        false,
        "2026-10-15 12:34:56",
        "2026-10-15T12:34:56.123456+03:00",
        "not-a-date",
        "2026-99-99",
        new DateTime(2026, 10, 15, 12, 34, 56),
        "Point(1 2)",
        "pg_sleep(10)",
        "Point('x')",
        " Point(1 2)",
        "point(1 2); trailing",
        JsonSerializer.SerializeToElement(1.12),
        JsonSerializer.SerializeToElement("2026-10-15"),
        Guid.Parse("b5ca8237-a14f-4f65-965d-9bd9634566b8"),
    ];

    public static IEnumerable<TestCaseData> LegacyCases()
    {
        foreach (var culture in new[] { "en-US", "de-DE", "he-IL", "" })
        foreach (var emptyMapping in new[] { false, true })
            for (var index = 0; index < LegacyValues.Length; index++)
                yield return new TestCaseData(culture, emptyMapping, index).SetName(
                    $"LegacyRow_{(culture.Length == 0 ? "Invariant" : culture)}_{(emptyMapping ? "Empty" : "Omitted")}_{index}"
                );
    }

    [TestCaseSource(nameof(LegacyCases))]
    public void WithoutColumnTypes_RowSqlMatchesPreChangeBehavior(
        string culture,
        bool emptyMapping,
        int index
    )
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var command = new Mock<IDbCommand>();
            command.SetupAllProperties();
            var connection = new Mock<IDbConnection>();
            connection.Setup(c => c.CreateCommand()).Returns(command.Object);
            var protocol = new Protocol(
                new SqlReaderConfig
                {
                    TableName = "measurements",
                    CommandTimeoutSeconds = 17,
                    ColumnTypes = emptyMapping ? new() : null,
                },
                connection.Object
            );
            var value = LegacyValues[index];
            using var rows = new DataTable();
            rows.Columns.Add("value", typeof(object));
            rows.Rows.Add(value ?? DBNull.Value);

            protocol.InsertRows(rows);

            Assert.Multiple(() =>
            {
                Assert.That(
                    command.Object.CommandText,
                    Is.EqualTo(
                        $"INSERT INTO measurements (value) VALUES ({PreColumnTypesLiteral(value)})"
                    )
                );
                Assert.That(command.Object.CommandTimeout, Is.EqualTo(17));
                connection.Verify(c => c.CreateCommand(), Times.Once);
                command.Verify(c => c.ExecuteNonQuery(), Times.Once);
                command.Verify(c => c.CreateParameter(), Times.Never);
                command.Verify(c => c.Dispose(), Times.Once);
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    // Frozen characterization oracle from 6263fcf, before this PR. Keep historical ordering,
    // quoting (including its limitations), culture sensitivity and UDT passthrough unchanged.
    private static object PreColumnTypesLiteral(object? value)
    {
        if (value == null || value == DBNull.Value)
            return "NULL";
        if (DateTime.TryParse(value.ToString(), out var time))
            return $"FORMAT('{time:yyyy-MM-dd HH:mm:ss}')";
        if (Regex.IsMatch(value.ToString()!, @"^\b\w+\b\((.*)\)"))
            return value;
        return $"'{value}'";
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WithoutColumnTypes_ChunkKeepsBulkPath_RawValues_Order_MetadataAndTimestamp(
        bool emptyMapping
    )
    {
        var connection = new Mock<IDbConnection>(MockBehavior.Strict);
        var protocol = new Protocol(
            new SqlReaderConfig { ColumnTypes = emptyMapping ? new() : null },
            connection.Object
        );
        var first = new Data<object>
        {
            Body = new JsonObject
            {
                ["price"] = 1.12,
                ["label"] = "O'Brien",
                ["optional"] = null,
            },
        };
        var second = new Data<object>
        {
            Body = new JsonObject
            {
                ["price"] = 2.34,
                ["label"] = "pg_sleep(10)",
                ["optional"] = null,
            },
        };
        var before = DateTime.UtcNow;
        var sent = protocol.SendChunk([first, second]).ToArray();
        var after = DateTime.UtcNow;

        Assert.Multiple(() =>
        {
            Assert.That(protocol.BulkData!.Rows.Count, Is.EqualTo(2));
            Assert.That(
                protocol.BulkData.Columns.Cast<DataColumn>().Select(c => c.ColumnName),
                Is.EqualTo(new[] { "price", "label", "optional" })
            );
            Assert.That(protocol.BulkData.Rows[0]["price"], Is.EqualTo(1.12));
            Assert.That(protocol.BulkData.Rows[0]["label"], Is.EqualTo("O'Brien"));
            Assert.That(protocol.BulkData.Rows[1]["label"], Is.EqualTo("pg_sleep(10)"));
            Assert.That(protocol.BulkData.Rows[0]["optional"], Is.EqualTo(DBNull.Value));
            Assert.That(sent.Select(d => d.Body), Is.EqualTo(new[] { first.Body, second.Body }));
            Assert.That(sent.All(d => d.Timestamp >= before && d.Timestamp <= after), Is.True);
            Assert.That(sent[0].Timestamp, Is.EqualTo(sent[1].Timestamp));
            connection.VerifyNoOtherCalls();
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WithoutColumnTypes_NullJsonStillFailsBeforeAnyDatabaseCall(bool emptyMapping)
    {
        var connection = new Mock<IDbConnection>(MockBehavior.Strict);
        var protocol = new Protocol(
            new SqlReaderConfig { ColumnTypes = emptyMapping ? new() : null },
            connection.Object
        );
        Assert.That(
            () => protocol.SendChunk([new Data<object>()]).ToList(),
            Throws.ArgumentException.With.Message.EqualTo("Can't send null json to sql table")
        );
        connection.VerifyNoOtherCalls();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WithoutColumnTypes_EmptyChunkStillCallsBulkWriter(bool emptyMapping)
    {
        var connection = new Mock<IDbConnection>(MockBehavior.Strict);
        var protocol = new Protocol(
            new SqlReaderConfig { ColumnTypes = emptyMapping ? new() : null },
            connection.Object
        );
        Assert.That(protocol.SendChunk([]), Is.Empty);
        Assert.That(protocol.BulkData, Is.Not.Null);
        Assert.That(protocol.BulkData!.Rows.Count, Is.Zero);
        connection.VerifyNoOtherCalls();
    }
}
