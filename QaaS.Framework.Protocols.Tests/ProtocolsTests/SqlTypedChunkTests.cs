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
            Assert.That(parameterOwner.Parameters[0].Value, Is.EqualTo("1.12"));
            Assert.That(parameterOwner.Parameters[1].Value, Is.EqualTo(label));
            Assert.That(
                parameterOwner
                    .Parameters.Cast<NpgsqlParameter>()
                    .All(p => p.NpgsqlDbType == NpgsqlDbType.Unknown),
                Is.True
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
            Is.EqualTo(new object[][] { ["1.12", "O'Brien"], [DBNull.Value, DBNull.Value] })
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
}
