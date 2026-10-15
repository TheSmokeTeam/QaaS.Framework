using System.Data;
using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Moq;
using Oracle.ManagedDataAccess.Client;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class SqlNativeChunkTests
{
    private sealed class MsSql(
        MsSqlSenderConfig config,
        IDbCommand command,
        IDbTransaction transaction
    ) : MsSqlProtocol(config, Globals.Logger)
    {
        protected override IDbCommand CreateDbCommand() => command;

        protected override IDbTransaction BeginChunkTransaction() => transaction;
    }

    private sealed class Oracle(
        OracleSenderConfig config,
        IDbCommand command,
        IDbTransaction transaction
    ) : OracleSqlProtocol(config, Globals.Logger)
    {
        protected override IDbCommand CreateDbCommand() => command;

        protected override IDbTransaction BeginChunkTransaction() => transaction;
    }

    [TestCase("mssql", "en-US")]
    [TestCase("mssql", "de-DE")]
    [TestCase("oracle", "en-US")]
    [TestCase("oracle", "de-DE")]
    public void UntypedChunkColumns_KeepNativeDateTimeAndBytes(string provider, string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var command = new Mock<IDbCommand>();
            command.SetupAllProperties();
            var transaction = new Mock<IDbTransaction>();
            var collection = new Mock<IDataParameterCollection>();
            var parameters = new List<IDbDataParameter>();
            command.SetupGet(c => c.Parameters).Returns(collection.Object);
            command
                .Setup(c => c.CreateParameter())
                .Returns(() => provider == "mssql" ? new SqlParameter() : new OracleParameter());
            collection
                .Setup(c => c.Add(It.IsAny<object>()))
                .Callback<object>(p => parameters.Add((IDbDataParameter)p))
                .Returns(0);
            IChunkSender protocol =
                provider == "mssql"
                    ? new MsSql(
                        new MsSqlSenderConfig
                        {
                            TableName = "measurements",
                            ColumnTypes = new() { ["price"] = "decimal(10, 2)" },
                        },
                        command.Object,
                        transaction.Object
                    )
                    : new Oracle(
                        new OracleSenderConfig
                        {
                            TableName = "measurements",
                            ColumnTypes = new() { ["price"] = "number(10, 2)" },
                        },
                        command.Object,
                        transaction.Object
                    );
            var observedAt = new DateTime(2026, 10, 15, 12, 34, 56, DateTimeKind.Utc);
            byte[] payload = [0, 1, 127, 255];

            protocol
                .SendChunk([
                    new Data<object>
                    {
                        Body = new JsonObject
                        {
                            ["price"] = 1.12,
                            ["observed_at"] = JsonValue.Create(observedAt),
                            ["payload"] = JsonValue.Create(payload),
                        },
                    },
                ])
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(parameters[0].Value, Is.EqualTo("1.12"));
                Assert.That(parameters[1].Value, Is.TypeOf<DateTime>().And.EqualTo(observedAt));
                Assert.That(parameters[2].Value, Is.SameAs(payload));
                Assert.That(
                    command.Object.CommandText,
                    Does.Contain(provider == "oracle" ? ":p1, :p2" : "@p1, @p2")
                );
                Assert.That(command.Object.Transaction, Is.SameAs(transaction.Object));
                transaction.Verify(t => t.Commit(), Times.Once);
                transaction.Verify(t => t.Rollback(), Times.Never);
                transaction.Verify(t => t.Dispose(), Times.Once);
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
