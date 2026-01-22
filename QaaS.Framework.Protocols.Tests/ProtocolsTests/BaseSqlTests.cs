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
        const int chunkSize = 5;
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
}