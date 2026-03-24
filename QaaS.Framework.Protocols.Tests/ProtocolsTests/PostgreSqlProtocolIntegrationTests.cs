using System.Text.Json.Nodes;
using Npgsql;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class PostgreSqlProtocolIntegrationTests
{
    private const string PostgisConnectionStringEnvironmentVariableName = "QAAS_POSTGIS_CONNECTION_STRING";

    private sealed record GeometryRoundTripExpectation(int Id, string Name, string Shape);
    private sealed record RepublishedGeometryRow(int Id, string SourceName, string ReplayName,
        string SourceGeometryText, string ReplayGeometryText, bool GeometryEquals, int ReplaySrid, string ReplayWkt);

    [Test]
    public void PostgreSqlProtocol_SendChunk_And_ReadChunk_RoundTripsPostGisGeometryColumns()
    {
        var connectionString = GetPostgisConnectionStringOrIgnore();
        var sourceTableName = $"public.qaas_geometry_roundtrip_{Guid.NewGuid():N}";
        var replayTableName = $"public.qaas_geometry_replay_{Guid.NewGuid():N}";
        PostgreSqlProtocol? sender = null;
        PostgreSqlProtocol? reader = null;
        PostgreSqlProtocol? replaySender = null;

        try
        {
            using var setupConnection = new NpgsqlConnection(connectionString);
            setupConnection.Open();
            RecreateGeometryTable(setupConnection, sourceTableName);
            RecreateGeometryTable(setupConnection, replayTableName);

            sender = new PostgreSqlProtocol(new PostgreSqlSenderConfig
            {
                ConnectionString = connectionString,
                TableName = sourceTableName
            }, Globals.Logger);
            reader = new PostgreSqlProtocol(new PostgreSqlReaderConfig
            {
                ConnectionString = connectionString,
                TableName = sourceTableName,
                InsertionTimeField = "created_at",
                IsInsertionTimeFieldTimeZoneTz = true,
                ReadFromRunStartTime = true,
                FilterSecondsBeforeRunStartTime = 1
            }, Globals.Logger);
            replaySender = new PostgreSqlProtocol(new PostgreSqlSenderConfig
            {
                ConnectionString = connectionString,
                TableName = replayTableName
            }, Globals.Logger);

            reader.Connect();
            sender.Connect();
            replaySender.Connect();

            var expectedRows = new[]
            {
                new GeometryRoundTripExpectation(7, "geometry-row-a",
                    "SRID=4326;POLYGON((35 31,35 32,36 32,36 31,35 31))"),
                new GeometryRoundTripExpectation(8, "geometry-row-b",
                    "SRID=4326;POLYGON((34.5 30.5,34.5 31.5,35.5 31.5,35.5 30.5,34.5 30.5))")
            };

            var sentRows = sender.SendChunk(expectedRows.Select(expectedRow => new Data<object>
            {
                Body = new
                {
                    id = expectedRow.Id,
                    name = expectedRow.Name,
                    shape = expectedRow.Shape
                }
            })).ToList();

            var receivedRows = reader.ReadChunk(TimeSpan.Zero).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(sentRows, Has.Count.EqualTo(expectedRows.Length));
                Assert.That(receivedRows, Has.Count.EqualTo(expectedRows.Length));
                Assert.That(receivedRows.All(row => row.Body is JsonObject), Is.True);
            });

            var replayedRows = replaySender.SendChunk(CreateReplayRows(receivedRows)).ToList();

            Assert.That(replayedRows, Has.Count.EqualTo(expectedRows.Length));
            AssertReceivedGeometryRows(connectionString, sourceTableName, replayTableName, receivedRows, expectedRows);
        }
        finally
        {
            SafeDisconnect(sender);
            SafeDisconnect(reader);
            SafeDisconnect(replaySender);

            using var cleanupConnection = new NpgsqlConnection(connectionString);
            cleanupConnection.Open();
            DropTableIfExists(cleanupConnection, sourceTableName);
            DropTableIfExists(cleanupConnection, replayTableName);
        }
    }

    private static string GetPostgisConnectionStringOrIgnore()
    {
        var connectionString = Environment.GetEnvironmentVariable(PostgisConnectionStringEnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(connectionString))
            Assert.Ignore(
                $"Set {PostgisConnectionStringEnvironmentVariableName} to run PostgreSQL/PostGIS integration tests.");

        return connectionString;
    }

    private static void RecreateGeometryTable(NpgsqlConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
                              CREATE EXTENSION IF NOT EXISTS postgis;
                              DROP TABLE IF EXISTS {tableName};
                              CREATE TABLE {tableName}
                              (
                                  id integer NOT NULL,
                                  name text NOT NULL,
                                  shape geometry(Polygon, 4326) NOT NULL,
                                  created_at timestamptz NOT NULL DEFAULT current_timestamp
                              );
                              """;
        command.ExecuteNonQuery();
    }

    private static void DropTableIfExists(NpgsqlConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS {tableName};";
        command.ExecuteNonQuery();
    }

    private static IEnumerable<Data<object>> CreateReplayRows(IEnumerable<DetailedData<object>> receivedRows)
    {
        return receivedRows.Select(receivedRow =>
        {
            var row = (JsonObject)receivedRow.Body!;

            return new Data<object>
            {
                Body = new
                {
                    id = row["id"]!.GetValue<int>(),
                    name = row["name"]!.GetValue<string>(),
                    shape = row["shape"]!.GetValue<string>()
                }
            };
        });
    }

    private static void AssertReceivedGeometryRows(string connectionString, string sourceTableName, string replayTableName,
        IEnumerable<DetailedData<object>> receivedRows, IEnumerable<GeometryRoundTripExpectation> expectedRows)
    {
        var receivedRowsById = receivedRows
            .Select(row => (JsonObject)row.Body!)
            .ToDictionary(row => row["id"]!.GetValue<int>());
        var republishedRowsById = LoadRepublishedGeometryRows(connectionString, sourceTableName, replayTableName)
            .ToDictionary(row => row.Id);

        Assert.Multiple(() =>
        {
            foreach (var expectedRow in expectedRows)
            {
                Assert.That(receivedRowsById.ContainsKey(expectedRow.Id), Is.True,
                    $"Missing row with id {expectedRow.Id}");
                Assert.That(republishedRowsById.ContainsKey(expectedRow.Id), Is.True,
                    $"Missing republished row with id {expectedRow.Id}");

                var receivedRow = receivedRowsById[expectedRow.Id];
                var receivedGeometryValue = receivedRow["shape"]?.GetValue<string>();
                var republishedRow = republishedRowsById[expectedRow.Id];

                Assert.That(receivedRow["name"]?.GetValue<string>(), Is.EqualTo(expectedRow.Name));
                Assert.That(receivedGeometryValue, Is.Not.Null.And.Not.Empty);
                Assert.That(receivedGeometryValue, Is.TypeOf<string>());
                Assert.That(republishedRow.SourceName, Is.EqualTo(expectedRow.Name));
                Assert.That(republishedRow.ReplayName, Is.EqualTo(expectedRow.Name));
                Assert.That(republishedRow.SourceGeometryText, Is.EqualTo(receivedGeometryValue));
                Assert.That(republishedRow.ReplayGeometryText, Is.EqualTo(receivedGeometryValue));
                Assert.That(republishedRow.GeometryEquals, Is.True);
                Assert.That(republishedRow.ReplaySrid, Is.EqualTo(4326));
                Assert.That(republishedRow.ReplayWkt, Is.EqualTo(StripSridPrefix(expectedRow.Shape)));
            }
        });
    }

    private static IReadOnlyCollection<RepublishedGeometryRow> LoadRepublishedGeometryRows(string connectionString,
        string sourceTableName, string replayTableName)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
                              SELECT source.id,
                                     source.name,
                                     replay.name,
                                     source.shape::text,
                                     replay.shape::text,
                                     ST_Equals(source.shape, replay.shape),
                                     ST_SRID(replay.shape),
                                     ST_AsText(replay.shape)
                              FROM {sourceTableName} source
                              JOIN {replayTableName} replay USING (id)
                              ORDER BY source.id;
                              """;

        using var dataReader = command.ExecuteReader();
        var rows = new List<RepublishedGeometryRow>();

        while (dataReader.Read())
        {
            rows.Add(new RepublishedGeometryRow(
                dataReader.GetInt32(0),
                dataReader.GetString(1),
                dataReader.GetString(2),
                dataReader.GetString(3),
                dataReader.GetString(4),
                dataReader.GetBoolean(5),
                dataReader.GetInt32(6),
                dataReader.GetString(7)));
        }

        return rows;
    }

    private static string StripSridPrefix(string geometryValue)
    {
        var separatorIndex = geometryValue.IndexOf(';');
        return separatorIndex >= 0
            ? geometryValue[(separatorIndex + 1)..]
            : geometryValue;
    }

    private static void SafeDisconnect(PostgreSqlProtocol? protocol)
    {
        if (protocol == null)
            return;

        try
        {
            protocol.Disconnect();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
