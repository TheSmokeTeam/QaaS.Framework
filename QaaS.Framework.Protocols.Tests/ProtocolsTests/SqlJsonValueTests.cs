using System.Data;
using System.Globalization;
using System.Text.Json.Nodes;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class SqlJsonValueTests
{
    private sealed class CapturingMsSql() : MsSqlProtocol(new MsSqlSenderConfig(), Globals.Logger)
    {
        public DataTable Table { get; private set; } = null!;
        public bool Fail { get; init; }
        public CultureInfo? BulkCulture { get; private set; }

        protected override void InsertChunkToTable(DataTable chunkData)
        {
            Table = chunkData;
            BulkCulture = CultureInfo.CurrentCulture;
            if (Fail)
                throw new DataException("bulk write failed");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SharedBulkSend_DoesNotChangeCallerCulture(bool fail)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            var callerCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentCulture = callerCulture;
            var protocol = new CapturingMsSql { Fail = fail };
            var data = new Data<object> { Body = JsonNode.Parse("""{"amount":1.25}""") };
            if (fail)
                Assert.Throws<DataException>(() => protocol.SendChunk([data]));
            else
                protocol.SendChunk([data]);
            Assert.Multiple(() =>
            {
                Assert.That(protocol.BulkCulture, Is.SameAs(callerCulture));
                Assert.That(CultureInfo.CurrentCulture, Is.SameAs(callerCulture));
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private sealed class CapturingPostgreSql()
        : PostgreSqlProtocol(new PostgreSqlSenderConfig(), Globals.Logger)
    {
        public string? DateText { get; private set; }

        protected override void InsertChunkToTable(DataTable chunkData) =>
            DateText = chunkData.Rows[0]["date"].ToString();
    }

    [Test]
    [SetCulture("en-GB")]
    public void PostgreSql_MixedJsonNumberAndNativeDate_KeepsDateFormatting()
    {
        var protocol = new CapturingPostgreSql();
        var date = new DateTime(2026, 11, 4);
        protocol.SendChunk([
            new Data<object>
            {
                Body = new JsonObject
                {
                    ["number"] = JsonNode.Parse("1.25"),
                    ["date"] = JsonValue.Create(date),
                },
            },
        ]);
        Assert.That(protocol.DateText, Is.EqualTo("04/11/2026 00:00:00"));
    }

    [Test]
    [SetCulture("de-DE")]
    public void MsSql_FailedBulkWrite_RestoresCallerCulture()
    {
        var callerCulture = CultureInfo.CurrentCulture;
        var protocol = new MsSqlProtocol(
            new MsSqlSenderConfig { TableName = "unconnected" },
            Globals.Logger
        );
        Assert.Throws<InvalidOperationException>(() =>
            protocol.SendChunk([new Data<object> { Body = JsonNode.Parse("""{"amount":1.25}""") }])
        );
        Assert.That(CultureInfo.CurrentCulture, Is.SameAs(callerCulture));
    }

    [TestCase(false)]
    [TestCase(true)]
    [SetCulture("en-GB")]
    public void MixedDateTextAndNativeDate_KeepsCallerDateFormatting(bool nativeFirst)
    {
        var protocol = new CapturingMsSql();
        Data<object>[] rows =
        [
            new Data<object>
            {
                Body = new JsonObject
                {
                    ["number"] = JsonNode.Parse("1.25"),
                    ["date"] = "04/11/2026 00:00:00",
                },
            },
            new Data<object>
            {
                Body = new JsonObject
                {
                    ["number"] = JsonNode.Parse("1.25"),
                    ["date"] = JsonValue.Create(new DateTime(2026, 11, 4)),
                },
            },
        ];
        protocol.SendChunk(nativeFirst ? rows.Reverse() : rows);
        var expected = nativeFirst ? (object)new DateTime(2026, 11, 4) : "04/11/2026 00:00:00";
        Assert.That(
            protocol.Table.Rows.Cast<DataRow>().Select(row => row["date"]),
            Is.All.EqualTo(expected)
        );
    }

    [Test]
    [SetCulture("de-DE")]
    public void NativeOnlyBulkSend_KeepsCallerCulture()
    {
        var protocol = new CapturingMsSql();
        protocol.SendChunk([new Data<object> { Body = new JsonObject { ["value"] = "1,25" } }]);
        Assert.Multiple(() =>
        {
            Assert.That(protocol.BulkCulture!.Name, Is.EqualTo("de-DE"));
            Assert.That(protocol.Table.Rows[0]["value"], Is.EqualTo("1,25"));
        });
    }

    [TestCase("parsed")]
    [TestCase("dictionary")]
    [TestCase("poco")]
    public void DefaultBulkSend_NormalizesJsonScalars(string source)
    {
        object body = source switch
        {
            "parsed" => JsonNode.Parse(
                """{"name":"O'Brien","amount":1.25,"active":true,"missing":null}"""
            )!,
            "dictionary" => new Dictionary<string, object?>
            {
                ["name"] = "O'Brien",
                ["amount"] = 1.25m,
                ["active"] = true,
                ["missing"] = null,
            },
            _ => new
            {
                name = "O'Brien",
                amount = 1.25m,
                active = true,
                missing = (object?)null,
            },
        };
        var protocol = new CapturingMsSql();
        var sent = protocol.SendChunk([new Data<object> { Body = body }]).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                protocol.Table.Rows[0].ItemArray,
                Is.EqualTo(new object[] { "O'Brien", "1.25", true, DBNull.Value })
            );
            Assert.That(protocol.Table.Columns["name"]!.DataType, Is.EqualTo(typeof(string)));
            Assert.That(sent.Single().Body, Is.SameAs(body));
        });
    }

    [TestCase("7")]
    [TestCase("1.25")]
    [TestCase("1234567890123456789012345678901234567890.123456789")]
    [TestCase("1e-100")]
    public void ParsedNumbers_KeepExactText(string literal)
    {
        var protocol = new CapturingMsSql();
        protocol.SendChunk([
            new Data<object> { Body = JsonNode.Parse("{\"value\":" + literal + "}") },
        ]);
        Assert.That(protocol.Table.Rows[0]["value"], Is.TypeOf<string>().And.EqualTo(literal));
    }

    [Test]
    public void NullFirstRow_AndNestedJson_ArePreserved()
    {
        var protocol = new CapturingMsSql();
        protocol.SendChunk([
            new Data<object>
            {
                Body = JsonNode.Parse("""{"value":null,"nested":{"x":1},"items":[1,null]}"""),
            },
            new Data<object>
            {
                Body = JsonNode.Parse("""{"value":"text","nested":null,"items":[]}"""),
            },
        ]);
        Assert.Multiple(() =>
        {
            Assert.That(protocol.Table.Rows[0]["value"], Is.EqualTo(DBNull.Value));
            Assert.That(protocol.Table.Rows[1]["value"], Is.EqualTo("text"));
            Assert.That(protocol.Table.Rows[0]["nested"], Is.EqualTo("{\"x\":1}"));
            Assert.That(protocol.Table.Rows[0]["items"], Is.EqualTo("[1,null]"));
            Assert.That(protocol.Table.Rows[1]["nested"], Is.EqualTo(DBNull.Value));
        });
    }

    [Test]
    public void MixedNumericRepresentations_DoNotCoerceValuesUsingCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var rows = new Data<object>[]
            {
                new() { Body = new JsonObject { ["value"] = 1.25m } },
                new() { Body = JsonNode.Parse("""{"value":1.25}""") },
            };
            foreach (var sequence in new[] { rows, rows.Reverse().ToArray() })
            {
                var protocol = new CapturingMsSql();
                protocol.SendChunk(sequence);
                var expected =
                    sequence == rows
                        ? new object[] { 1.25m, 1.25m }
                        : new object[] { "1.25", "1.25" };
                Assert.That(
                    protocol.Table.Rows.Cast<DataRow>().Select(row => row["value"]),
                    Is.EqualTo(expected)
                );
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Test]
    public void NativeJsonValues_KeepTheirClrTypes()
    {
        var protocol = new CapturingMsSql();
        var time = new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);
        byte[] bytes = [1, 2, 255];
        protocol.SendChunk([
            new Data<object>
            {
                Body = new JsonObject
                {
                    ["id"] = 7,
                    ["time"] = JsonValue.Create(time),
                    ["bytes"] = JsonValue.Create(bytes),
                    ["flag"] = false,
                },
            },
        ]);
        Assert.Multiple(() =>
        {
            Assert.That(protocol.Table.Rows[0]["id"], Is.TypeOf<int>().And.EqualTo(7));
            Assert.That(protocol.Table.Rows[0]["time"], Is.TypeOf<DateTime>().And.EqualTo(time));
            Assert.That(protocol.Table.Rows[0]["bytes"], Is.SameAs(bytes));
            Assert.That(protocol.Table.Rows[0]["flag"], Is.EqualTo(false));
        });
    }
}
