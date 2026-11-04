using System.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class SqlThroughputTests
{
    private sealed class OracleRows(OracleSenderConfig config, IDbCommand command)
        : OracleSqlProtocol(config, NullLogger.Instance)
    {
        protected override IDbCommand CreateDbCommand() => command;

        protected override IDbTransaction BeginChunkTransaction() => Mock.Of<IDbTransaction>();
    }

    [TestCase("CLOB", OracleDbType.Clob)]
    [TestCase("NCLOB", OracleDbType.NClob)]
    [TestCase("BLOB", OracleDbType.Blob)]
    public void OracleLob_IsBoundWithoutUnsupportedCast(string type, OracleDbType expected)
    {
        using var owner = new OracleCommand();
        var command = new Mock<IDbCommand>();
        command.SetupAllProperties();
        command.SetupGet(x => x.Parameters).Returns(owner.Parameters);
        command.Setup(x => x.CreateParameter()).Returns(() => new OracleParameter());
        var protocol = new OracleRows(
            new OracleSenderConfig
            {
                TableName = "items",
                ColumnTypes = new() { ["payload"] = type },
            },
            command.Object
        );
        var body = type == "BLOB" ? (object)new byte[] { 1, 2 } : new string('x', 1024 * 1024);
        protocol.SendChunk([new Data<object> { Body = new { payload = body } }]).ToArray();
        Assert.That(
            command.Object.CommandText,
            Is.EqualTo("INSERT INTO items (payload) VALUES (:p0)")
        );
        Assert.That(owner.Parameters[0].OracleDbType, Is.EqualTo(expected));
        Assert.That(owner.Parameters[0].Value, Is.Not.Null);
    }

    private sealed class QueryProtocol(SqlReaderConfig config, IDbConnection connection)
        : BaseSqlProtocol<IDbConnection>(config, NullLogger.Instance, connection)
    {
        public void SendRow(Data<object> data) =>
            RowInsertIntoTable(GetDataTableFromRawDataChunk([data]));

        protected override void InsertChunkToTable(DataTable data) =>
            throw new NotSupportedException();

        protected override string GetTableQueryWithoutRegardToInsertionTimeField() =>
            $"select {ReadProjection} from items";

        protected override string GetTableQueryArrangedByInsertionTimeFieldAsc() =>
            GetTableQueryWithoutRegardToInsertionTimeField();

        protected override string GetLatestTableRowQuery() => throw new NotSupportedException();

        protected override string GetTimeFieldSqlFormat(DateTime time) => "legacy-date";
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Read_StreamingIsOptIn_AndDisposalClosesReader(bool stream)
    {
        var data = new DataTable();
        data.Columns.Add("payload", typeof(string));
        data.Rows.Add("first");
        data.Rows.Add("second");
        var reader = data.CreateDataReader();
        var command = new Mock<IDbCommand>();
        command.SetupAllProperties();
        command.Setup(x => x.ExecuteReader()).Returns(reader);
        var connection = new Mock<IDbConnection>();
        connection.Setup(x => x.CreateCommand()).Returns(command.Object);
        connection.SetupGet(x => x.State).Returns(ConnectionState.Open);
        var protocol = new QueryProtocol(
            new SqlReaderConfig
            {
                TableName = "items",
                StreamResults = stream,
                ColumnsToRead = ["payload"],
            },
            connection.Object
        );
        var result = protocol.ReadChunk(TimeSpan.Zero);
        command.Verify(x => x.ExecuteReader(), stream ? Times.Never() : Times.Once());
        using (var iterator = result.GetEnumerator())
        {
            Assert.That(iterator.MoveNext(), Is.True);
        }
        Assert.That(reader.IsClosed, Is.True);
        connection.Verify(x => x.Close(), Times.Once);
        Assert.That(command.Object.CommandText, Is.EqualTo("select \"payload\" from items"));
    }

    [Test]
    public void ParameterizedRows_TreatQuotesAndExpressionsAsData()
    {
        using var owner = new NpgsqlCommand();
        var command = new Mock<IDbCommand>();
        command.SetupAllProperties();
        command.SetupGet(x => x.Parameters).Returns(owner.Parameters);
        command.Setup(x => x.CreateParameter()).Returns(() => new NpgsqlParameter());
        var connection = new Mock<IDbConnection>();
        connection.Setup(x => x.CreateCommand()).Returns(command.Object);
        var protocol = new QueryProtocol(
            new SqlReaderConfig { TableName = "items", ParameterizeRows = true },
            connection.Object
        );
        protocol.SendRow(new Data<object> { Body = new { payload = "Point('quoted')" } });
        Assert.That(
            command.Object.CommandText,
            Is.EqualTo("INSERT INTO items (payload) VALUES (@p0)")
        );
        Assert.That(owner.Parameters[0].Value, Is.EqualTo("Point('quoted')"));
    }
}
