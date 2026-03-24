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
    private const string GeometryValue = "SRID=4326;POLYGON((35 31,35 32,36 32,36 31,35 31))";

    [Test]
    public void PostgreSqlProtocol_SendChunk_And_ReadChunk_RoundTripsPostGisGeometryColumns()
    {
        var connectionString = GetPostgisConnectionStringOrIgnore();
        var tableName = $"public.qaas_geometry_roundtrip_{Guid.NewGuid():N}";
        PostgreSqlProtocol? sender = null;
        PostgreSqlProtocol? reader = null;

        try
        {
            using var setupConnection = new NpgsqlConnection(connectionString);
            setupConnection.Open();
            RecreateGeometryTable(setupConnection, tableName);

            sender = new PostgreSqlProtocol(new PostgreSqlSenderConfig
            {
                ConnectionString = connectionString,
                TableName = tableName
            }, Globals.Logger);
            reader = new PostgreSqlProtocol(new PostgreSqlReaderConfig
            {
                ConnectionString = connectionString,
                TableName = tableName,
                InsertionTimeField = "created_at",
                IsInsertionTimeFieldTimeZoneTz = true,
                ReadFromRunStartTime = true,
                FilterSecondsBeforeRunStartTime = 1
            }, Globals.Logger);

            reader.Connect();
            sender.Connect();

            var sentRows = sender.SendChunk([
                new Data<object>
                {
                    Body = new
                    {
                        id = 7,
                        name = "geometry-row",
                        shape = GeometryValue
                    }
                }
            ]).ToList();

            var receivedRows = reader.ReadChunk(TimeSpan.Zero).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(sentRows, Has.Count.EqualTo(1));
                Assert.That(receivedRows, Has.Count.EqualTo(1));
                Assert.That(receivedRows[0].Body, Is.TypeOf<JsonObject>());
            });

            var row = (JsonObject)receivedRows[0].Body!;
            var geometryValue = row["shape"]?.GetValue<string>();

            Assert.Multiple(() =>
            {
                Assert.That(row["id"]?.GetValue<int>(), Is.EqualTo(7));
                Assert.That(row["name"]?.GetValue<string>(), Is.EqualTo("geometry-row"));
                Assert.That(geometryValue, Is.Not.Null.And.Not.Empty);
            });
        }
        finally
        {
            SafeDisconnect(sender);
            SafeDisconnect(reader);

            using var cleanupConnection = new NpgsqlConnection(connectionString);
            cleanupConnection.Open();
            DropTableIfExists(cleanupConnection, tableName);
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
