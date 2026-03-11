using System.Collections.Immutable;
using System.Data;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Moq;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

/// <summary>
/// Class for testing `BaseSqlDataBaseSendr` functionalities 
/// </summary>
internal class MockSqlProtocol : BaseSqlProtocol<IDbConnection>
{
    public int InsertChunkToTableCalls = 0;

    public MockSqlProtocol(SqlReaderConfig configurations, ILogger logger,
        IDbConnection? dbConnection = null) : base(configurations, logger, dbConnection)
    {
    }

    public MockSqlProtocol(string name, SqlConfig configurations, ILogger logger, IDbConnection? dbConnection = null) :
        base(configurations, logger, dbConnection)
    {
    }

    protected override void InsertChunkToTable(DataTable chunkData)
    {
        InsertChunkToTableCalls += chunkData.Rows.Count;
    }

    protected override string GetTableQueryArrangedByInsertionTimeFieldAsc()
    {
        return string.Empty;
    }

    protected override string GetTableQueryWithoutRegardToInsertionTimeField()
    {
        return string.Empty;
    }

    protected override string GetLatestTableRowQuery()
    {
        return string.Empty;
    }

    protected override long? GetNumberOfMilliSecondsPassedSinceLastTableChange()
    {
        return 0;
    }

    protected override string GetTimeFieldSqlFormat(DateTime time)
    {
        return string.Empty;
    }
}

internal sealed class SequencedMockSqlProtocol : MockSqlProtocol
{
    private readonly Queue<long?> _elapsedMillisecondsByPoll;

    public SequencedMockSqlProtocol(SqlReaderConfig configurations, ILogger logger,
        IEnumerable<long?> elapsedMillisecondsByPoll, IDbConnection? dbConnection = null)
        : base(configurations, logger, dbConnection)
    {
        _elapsedMillisecondsByPoll = new Queue<long?>(elapsedMillisecondsByPoll);
    }

    protected override long? GetNumberOfMilliSecondsPassedSinceLastTableChange()
    {
        return _elapsedMillisecondsByPoll.Count > 0
            ? _elapsedMillisecondsByPoll.Dequeue()
            : 0;
    }
}

internal sealed class CollectingLogger : ILogger
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

[TestFixture]
public class BaseSqlTests
{
    private static Mock<IDbConnection>? _dbConnectionMock;
    private static Mock<IDbCommand>? _dbCommandMock;
    private static Mock<IDataReader>? _dataReaderMock;

    private static readonly MethodInfo GetJsonEnumerableFromQuery = typeof(MockSqlProtocol).GetMethod(
        "GetJsonEnumerableFromQuery", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [SetUp]
    public void SetUp()
    {
        _dataReaderMock = new Mock<IDataReader>();
        _dataReaderMock.Setup(mock => mock.Read()).Returns(false).Verifiable();

        _dbCommandMock = new Mock<IDbCommand>();
        _dbCommandMock.Setup(mock => mock.ExecuteNonQuery()).Verifiable();
        _dbCommandMock.Setup(mock => mock.ExecuteReader()).Returns(_dataReaderMock.Object).Verifiable();

        _dbConnectionMock = new Mock<IDbConnection>();
        _dbConnectionMock.Setup(mock => mock.CreateCommand()).Returns(_dbCommandMock.Object)
            .Verifiable();
        _dbConnectionMock.Setup(mock => mock.Open()).Verifiable();
        _dbConnectionMock.Setup(mock => mock.Close()).Verifiable();
        _dbConnectionMock.Setup(mock => mock.Dispose()).Verifiable();
    }

    [Test]
    public void TestGetJsonEnumerableFromQuery_CallFunctionWithEmptyReaderMock_ShouldNotReturnAnyOutput()
    {
        // Arrange
        var mockBaseSqlDataBaseSender =
            new MockSqlProtocol("test", new SqlReaderConfig(), Globals.Logger, _dbConnectionMock!.Object);

        // Act
        var result = ((IEnumerable<JsonNode>)GetJsonEnumerableFromQuery.Invoke(mockBaseSqlDataBaseSender,
            ["random query", 30, null])!).ToList();

        // Assert
        _dbCommandMock!.Verify(command => command.ExecuteReader(), Times.Once);
        Assert.That(result, Is.Empty);
    }

    [Test,
     TestCase(null, 100),
     TestCase(1, 1),
     TestCase(100, 5),
     TestCase(5, 100),
     TestCase(11, 100)]
    public void
        TestSend_CallSendFunctionWithDifferentChunkToDataSizeRatios_ShouldCallSendChunkFunctionRatioAmountOfTimesAndReturnDataSizeAmountOfItems
        (int? chunkSize, int dataSize)
    {
        // Arrange
        var data = new List<Data<object>>(dataSize);
        for (var dataIndex = 0; dataIndex < dataSize; dataIndex++)
        {
            data.Add(new Data<object> { Body = new JsonObject() });
        }

        var sqlTableSenderMock =
            new MockSqlProtocol("test", new SqlConfig(), Globals.Logger, _dbConnectionMock!.Object);

        // Act
        var itemsSent = sqlTableSenderMock.SendChunk(data.ToImmutableList()).ToArray();

        // Assert
        var dataSizeToChunkSizeRatio = dataSize / chunkSize ?? 1;
        var dataSizeToChunkLeftOver = dataSize % chunkSize ?? 0;
        Assert.Multiple(() =>
        {
            Assert.That(sqlTableSenderMock.InsertChunkToTableCalls, Is.EqualTo(dataSize));
            Assert.That(itemsSent, Has.Length.EqualTo(dataSize));
        });
    }

    [Test]
    public void TestSend_CallSendFunctionWithNullDataBody_ShouldThrowException()
    {
        // Arrange
        const int dataSize = 25;
        var data = new List<Data<object>>(dataSize);
        for (var dataIndex = 0; dataIndex < dataSize; dataIndex++)
        {
            data.Add(new Data<object> { Body = null });
        }

        var sqlTableSenderMock =
            new MockSqlProtocol("test", new SqlConfig(), Globals.Logger, _dbConnectionMock!.Object);

        // Act + Assert
        Assert.Throws<ArgumentException>(() => sqlTableSenderMock.SendChunk(data.ToImmutableList()).ToArray());
    }

    [Test]
    public void Connect_And_Disconnect_OpenCloseAndDisposeConnection()
    {
        var protocol = new MockSqlProtocol(
            "test",
            new SqlConfig
            {
                TableName = "tbl",
                ConnectionString = "Host=localhost"
            },
            Globals.Logger,
            _dbConnectionMock!.Object);

        protocol.Connect();
        protocol.Disconnect();

        _dbConnectionMock.Verify(connection => connection.Open(), Times.Once);
        _dbConnectionMock.Verify(connection => connection.Close(), Times.Once);
        _dbConnectionMock.Verify(connection => connection.Dispose(), Times.Once);
    }

    [Test]
    public void ReadChunk_WithSingleRow_ReturnsDetailedData()
    {
        var now = DateTime.UtcNow;
        _dataReaderMock!.SetupSequence(mock => mock.Read())
            .Returns(true)
            .Returns(false);
        _dataReaderMock.Setup(mock => mock.FieldCount).Returns(1);
        _dataReaderMock.Setup(mock => mock.GetName(0)).Returns("created_at");
        _dataReaderMock.Setup(mock => mock.GetValue(0)).Returns(now);
        _dbConnectionMock!.SetupGet(connection => connection.State).Returns(ConnectionState.Open);

        var protocol = new MockSqlProtocol(
            new SqlReaderConfig
            {
                TableName = "tbl",
                ConnectionString = "Host=localhost",
                InsertionTimeField = "created_at"
            },
            Globals.Logger,
            _dbConnectionMock.Object);

        var result = protocol.ReadChunk(TimeSpan.Zero).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Body, Is.TypeOf<JsonObject>());
        Assert.That(result[0].Timestamp, Is.Not.Null);
    }

    [Test]
    public void ReadChunk_WhenPollingForTimeout_LogsSingleSummaryDebugMessage()
    {
        var logger = new CollectingLogger();
        _dbConnectionMock!.SetupGet(connection => connection.State).Returns(ConnectionState.Open);
        var protocol = new SequencedMockSqlProtocol(
            new SqlReaderConfig
            {
                TableName = "tbl",
                ConnectionString = "Host=localhost",
                InsertionTimeField = "created_at"
            },
            logger,
            [0, 0, 5],
            _dbConnectionMock.Object);

        _ = protocol.ReadChunk(TimeSpan.FromMilliseconds(5)).ToList();

        var debugMessages = logger.Entries
            .Where(entry => entry.Level == LogLevel.Debug)
            .Select(entry => entry.Message)
            .ToList();

        Assert.That(debugMessages, Has.Count.EqualTo(1));
        Assert.That(debugMessages[0], Does.Contain("Read timeout reached for table tbl"));
    }

    [Test]
    public void RowInsertIntoTable_ExecutesInsertStatements()
    {
        _dbCommandMock!.SetupSet(command => command.CommandText = It.Is<string>(text =>
            text.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase))).Verifiable();

        var protocol = new MockSqlProtocol(
            "target_table",
            new SqlConfig
            {
                TableName = "target_table",
                ConnectionString = "Host=localhost"
            },
            Globals.Logger,
            _dbConnectionMock!.Object);

        var dataTable = new DataTable();
        dataTable.Columns.Add("id", typeof(int));
        dataTable.Columns.Add("name", typeof(string));
        dataTable.Rows.Add(1, "alice");

        var rowInsertMethod = typeof(BaseSqlProtocol<IDbConnection>).GetMethod(
            "RowInsertIntoTable",
            BindingFlags.Instance | BindingFlags.NonPublic);
        rowInsertMethod!.Invoke(protocol, [dataTable]);

        _dbCommandMock.Verify(command => command.ExecuteNonQuery(), Times.Once);
        _dbCommandMock.VerifySet(command => command.CommandText = It.IsAny<string>(), Times.AtLeastOnce);
    }
}
