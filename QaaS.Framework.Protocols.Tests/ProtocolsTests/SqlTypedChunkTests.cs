using System.Data;
using System.Text.Json.Nodes;
using Moq;
using Npgsql;
using NpgsqlTypes;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class SqlTypedChunkTests
{
    private sealed class Protocol(
        PostgreSqlSenderConfig config,
        IDbCommand command,
        IDbTransaction? transaction = null
    ) : PostgreSqlProtocol(config, Globals.Logger)
    {
        protected override IDbCommand CreateDbCommand() => command;

        protected override IDbTransaction BeginChunkTransaction() =>
            transaction ?? Mock.Of<IDbTransaction>();
    }

    [TestCase("O'Brien")]
    [TestCase("pg_sleep(10)")]
    [TestCase("'); DROP TABLE measurements; --")]
    [TestCase("1.12")]
    [TestCase("2026-10-15 12:34:56")]
    [TestCase("a\\b;c\nnext")]
    public void TypedChunk_UntypedValuesRemainData(string label)
    {
        var command = new Mock<IDbCommand>();
        command.SetupAllProperties();
        using var parameterOwner = new NpgsqlCommand();
        command.SetupGet(c => c.Parameters).Returns(parameterOwner.Parameters);
        command.Setup(c => c.CreateParameter()).Returns(() => new NpgsqlParameter());
        var protocol = new Protocol(
            new PostgreSqlSenderConfig
            {
                TableName = "measurements",
                ColumnTypes = new() { ["price"] = "double precision" },
            },
            command.Object
        );

        protocol
            .SendChunk([
                new Data<object>
                {
                    Body = new JsonObject { ["price"] = 1.12, ["label"] = label },
                },
            ])
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(
                command.Object.CommandText,
                Is.EqualTo(
                    "INSERT INTO measurements (price, label) VALUES (CAST(@p0 AS double precision), @p1)"
                )
            );
            Assert.That(parameterOwner.Parameters[0].Value, Is.TypeOf<double>().And.EqualTo(1.12));
            Assert.That(parameterOwner.Parameters[1].Value, Is.EqualTo(label));
            Assert.That(
                parameterOwner.Parameters[1].NpgsqlDbType,
                Is.EqualTo(NpgsqlDbType.Unknown)
            );
        });
    }

    [Test]
    public void TypedChunk_RebindsParametersForEveryRow_AndPreservesNulls()
    {
        var command = new Mock<IDbCommand>();
        command.SetupAllProperties();
        using var parameterOwner = new NpgsqlCommand();
        command.SetupGet(c => c.Parameters).Returns(parameterOwner.Parameters);
        command.Setup(c => c.CreateParameter()).Returns(() => new NpgsqlParameter());
        var values = new List<object[]>();
        command
            .Setup(c => c.ExecuteNonQuery())
            .Callback(() =>
                values.Add(
                    parameterOwner
                        .Parameters.Cast<NpgsqlParameter>()
                        .Select(p => p.Value!)
                        .ToArray()
                )
            )
            .Returns(1);
        var protocol = new Protocol(
            new PostgreSqlSenderConfig
            {
                TableName = "measurements",
                ColumnTypes = new() { ["price"] = "numeric" },
            },
            command.Object
        );

        protocol
            .SendChunk([
                new Data<object>
                {
                    Body = new JsonObject { ["price"] = 1.12, ["label"] = "O'Brien" },
                },
                new Data<object>
                {
                    Body = new JsonObject { ["price"] = null, ["label"] = null },
                },
            ])
            .ToList();

        Assert.That(
            values,
            Is.EqualTo(new object[][] { [1.12, "O'Brien"], [DBNull.Value, DBNull.Value] })
        );
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TypedChunk_FailedLaterRow_RollsBackWholeChunk_AndPreservesOriginalError(
        bool rollbackFails
    )
    {
        var command = new Mock<IDbCommand>();
        command.SetupAllProperties();
        using var parameterOwner = new NpgsqlCommand();
        command.SetupGet(c => c.Parameters).Returns(parameterOwner.Parameters);
        command.Setup(c => c.CreateParameter()).Returns(() => new NpgsqlParameter());
        var failure = new DataException("second row failed");
        command.SetupSequence(c => c.ExecuteNonQuery()).Returns(1).Throws(failure);
        var transaction = new Mock<IDbTransaction>();
        if (rollbackFails)
            transaction
                .Setup(t => t.Rollback())
                .Throws(new InvalidOperationException("connection lost"));
        var protocol = new Protocol(
            new PostgreSqlSenderConfig
            {
                TableName = "measurements",
                ColumnTypes = new() { ["price"] = "numeric" },
            },
            command.Object,
            transaction.Object
        );
        var rows = Enumerable
            .Range(0, 3)
            .Select(i => new Data<object> { Body = new JsonObject { ["price"] = i } });

        Assert.That(
            Assert.Throws<DataException>(() => protocol.SendChunk(rows).ToList()),
            Is.SameAs(failure)
        );
        Assert.Multiple(() =>
        {
            Assert.That(command.Object.Transaction, Is.SameAs(transaction.Object));
            command.Verify(c => c.ExecuteNonQuery(), Times.Exactly(2));
            transaction.Verify(t => t.Commit(), Times.Never);
            transaction.Verify(t => t.Rollback(), Times.Once);
            transaction.Verify(t => t.Dispose(), Times.Once);
            command.Verify(c => c.Dispose(), Times.Once);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PostgreSql_ExplicitNativeTypes_KeepBinaryAndTemporalValues(bool chunk)
    {
        var command = new Mock<IDbCommand>();
        command.SetupAllProperties();
        using var parameterOwner = new NpgsqlCommand();
        command.SetupGet(c => c.Parameters).Returns(parameterOwner.Parameters);
        command.Setup(c => c.CreateParameter()).Returns(() => new NpgsqlParameter());
        var protocol = new Protocol(
            new PostgreSqlSenderConfig
            {
                TableName = "measurements",
                ColumnTypes = new()
                {
                    ["observed_at"] = "timestamp with time zone",
                    ["payload"] = "bytea",
                    ["offset_at"] = "timestamp with time zone",
                },
            },
            command.Object
        );
        var observedAt = new DateTime(2026, 10, 15, 12, 34, 56, DateTimeKind.Utc);
        var offsetAt = new DateTimeOffset(2026, 10, 15, 12, 34, 56, TimeSpan.FromHours(3));
        byte[] payload = [0, 1, 127, 255];
        var data = new Data<object>
        {
            Body = new JsonObject
            {
                ["observed_at"] = JsonValue.Create(observedAt),
                ["payload"] = JsonValue.Create(payload),
                ["offset_at"] = JsonValue.Create(offsetAt),
            },
        };

        if (chunk)
            protocol.SendChunk([data]).ToList();
        else
            protocol.Send(data);

        Assert.Multiple(() =>
        {
            Assert.That(
                parameterOwner.Parameters[0].Value,
                Is.TypeOf<DateTime>().And.EqualTo(observedAt)
            );
            Assert.That(parameterOwner.Parameters[1].Value, Is.SameAs(payload));
            Assert.That(parameterOwner.Parameters[1].NpgsqlDbType, Is.EqualTo(NpgsqlDbType.Bytea));
            Assert.That(parameterOwner.Parameters[2].Value, Is.EqualTo(offsetAt.ToUniversalTime()));
            Assert.That(
                ((DateTimeOffset)parameterOwner.Parameters[2].Value!).Offset,
                Is.EqualTo(TimeSpan.Zero)
            );
            Assert.That(command.Object.CommandText, Does.Contain("CAST(@p1 AS bytea)"));
        });
    }
}
