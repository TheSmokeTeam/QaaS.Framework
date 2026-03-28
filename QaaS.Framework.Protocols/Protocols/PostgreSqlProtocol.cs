using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Logging;
using Npgsql;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Framework.Protocols.Protocols;

[ExcludeFromCodeCoverage]
public class PostgreSqlProtocol : BaseSqlProtocol<NpgsqlConnection>, ISender
{
    private sealed record UnknownResultTypeInspection(bool InspectionSucceeded, bool[]? UnknownResultTypeList);
    private sealed record UnknownResultTypeCacheEntry(bool[]? UnknownResultTypeList);

    private readonly bool _isInsertionTimeFieldTimeZoneTz;
    private readonly ConcurrentDictionary<string, UnknownResultTypeCacheEntry> _unknownResultTypeCache =
        new(StringComparer.Ordinal);

    public PostgreSqlProtocol(PostgreSqlReaderConfig configurations, ILogger logger,
        NpgsqlConnection? dbConnection = null,
        string? timeZoneId = null) : base(configurations, logger,
        dbConnection ?? new NpgsqlConnection(configurations.ConnectionString), timeZoneId)
    {
        _isInsertionTimeFieldTimeZoneTz = configurations.IsInsertionTimeFieldTimeZoneTz;
    }

    public PostgreSqlProtocol(PostgreSqlSenderConfig configurations, ILogger logger,
        NpgsqlConnection? dbConnection = null,
        string? timeZoneId = null) : base(configurations,
        logger, dbConnection ?? new NpgsqlConnection(configurations.ConnectionString), timeZoneId)
    {
    }

    protected override void InsertChunkToTable(DataTable chunkData)
    {
        const string nullValueRepresentation = "\\N";
        // Get the column names they can be listed in the Copy query in the same order as they are ordered under the data table
        var columnNames = from DataColumn column in chunkData.Columns select column.ColumnName;
        using var writer = DbConnection.BeginTextImport(
            $"COPY {TableName} ({string.Join(", ", columnNames)}) FROM STDIN" +
            $" (FORMAT text, DELIMITER ';', NULL '{nullValueRepresentation}')");
        foreach (DataRow row in chunkData.Rows)
        {
            var rowBuilder = new StringBuilder();
            var firstColumn = true;
            foreach (DataColumn column in chunkData.Columns)
            {
                if (!firstColumn)
                {
                    rowBuilder.Append(';');
                }

                // Escape all native occurrences of delimiter ;
                var columnValue = row[column];
                var nullableColumnValue = columnValue == DBNull.Value
                    ? nullValueRepresentation
                    : columnValue.ToString()?.Replace(";", "\\;");
                rowBuilder.Append(nullableColumnValue);
                firstColumn = false;
            }

            writer.WriteLine(rowBuilder.ToString());
        }

        writer.Close();
        Logger.LogDebug("Inserted {RowCount} rows into postgresql table {TableName}", chunkData.Rows.Count, TableName);
    }

    public override void Connect()
    {
        base.Connect();
        using var cmd = new NpgsqlCommand($"SET statement_timeout = {CommandTimeoutSeconds * 1000}",
            (NpgsqlConnection?)DbConnection);
        cmd.ExecuteNonQuery();
    }

    protected override IDataReader ExecuteReader(IDbCommand command)
    {
        if (command is not NpgsqlCommand npgsqlCommand)
            return base.ExecuteReader(command);

        var queryText = npgsqlCommand.CommandText ?? string.Empty;
        if (!_unknownResultTypeCache.TryGetValue(queryText, out var cacheEntry))
        {
            var inspection = InspectUnknownResultTypes(npgsqlCommand);
            if (inspection.InspectionSucceeded)
            {
                cacheEntry = new UnknownResultTypeCacheEntry(inspection.UnknownResultTypeList);
                _unknownResultTypeCache[queryText] = cacheEntry;
            }
        }

        if (cacheEntry?.UnknownResultTypeList != null)
            npgsqlCommand.UnknownResultTypeList = cacheEntry.UnknownResultTypeList;

        return ExecutePostgreSqlReader(npgsqlCommand);
    }

    /// <inheritdoc />
    protected override string GetTableQueryArrangedByInsertionTimeFieldAsc() =>
        $"select * from {TableName} {BuildWhereStatement()} order by \"{InsertionTimeField}\" asc";

    /// <inheritdoc />
    protected override string GetTableQueryWithoutRegardToInsertionTimeField() =>
        $"select * from {TableName} {BuildWhereStatement()}";

    private string GetOneRowFromTableQueryWithoutRegardToInsertionTimeField() =>
        $"select * from {TableName} {BuildWhereStatement()} LIMIT 1";

    /// <inheritdoc />
    protected override string GetLatestTableRowQuery() =>
        $"select * from {TableName} {BuildWhereStatement()} order by \"{InsertionTimeField}\" desc LIMIT 1";

    protected override string GetTimeFieldSqlFormat(DateTime time) =>
        $"TO_TIMESTAMP('{time:dd-MM-yyyy HH:mm:ss}', 'DD/MM/YYYY hh24:mi:ss')";

    private string BuildWhereStatement()
    {
        if (WhereStatement == null)
            return FilterFromStartTime
                ? $"where {BuildInsertionTimeFieldName()} > '{StartTimeDbTimeZone:O}'::timestamp"
                : string.Empty;
        return FilterFromStartTime
            ? $"where ({BuildInsertionTimeFieldName()} > '{StartTimeDbTimeZone:O}'::timestamp) and ({WhereStatement})"
            : $"where {WhereStatement}";
    }

    private string BuildInsertionTimeFieldName() => _isInsertionTimeFieldTimeZoneTz
        ? $"\"{InsertionTimeField}\" AT TIME ZONE 'UTC'"
        : $"\"{InsertionTimeField}\"";

    protected virtual IDataReader ExecutePostgreSqlReader(NpgsqlCommand command) => command.ExecuteReader();

    protected virtual IDataReader ExecuteSchemaReader(NpgsqlCommand command) =>
        command.ExecuteReader(CommandBehavior.SchemaOnly);

    private UnknownResultTypeInspection InspectUnknownResultTypes(NpgsqlCommand command)
    {
        try
        {
            using var schemaReader = ExecuteSchemaReader(command);
            if (schemaReader.FieldCount == 0)
                return new UnknownResultTypeInspection(true, null);

            var unknownResultTypes = new bool[schemaReader.FieldCount];
            var hasUnknownResultTypes = false;

            for (var col = 0; col < schemaReader.FieldCount; col++)
            {
                var dataTypeName = schemaReader.GetDataTypeName(col);
                if (!ShouldReadResultColumnAsText(dataTypeName))
                    continue;

                unknownResultTypes[col] = true;
                hasUnknownResultTypes = true;
                Logger.LogDebug(
                    "Requesting PostgreSQL column {ColumnName} with data type {DataTypeName} as text",
                    schemaReader.GetName(col), dataTypeName);
            }

            return new UnknownResultTypeInspection(true, hasUnknownResultTypes ? unknownResultTypes : null);
        }
        catch (Exception exception)
        {
            Logger.LogDebug(exception,
                "Failed to inspect PostgreSQL result types for query {QueryCommand}; using default result mapping",
                command.CommandText);
            return new UnknownResultTypeInspection(false, null);
        }
    }

    private static bool ShouldReadResultColumnAsText(string? dataTypeName)
    {
        if (string.IsNullOrWhiteSpace(dataTypeName))
            return false;

        return dataTypeName.Contains('.', StringComparison.Ordinal) &&
               !dataTypeName.StartsWith("pg_catalog.", StringComparison.OrdinalIgnoreCase);
    }

    public DetailedData<object> Send(Data<object> dataToSend)
    {
        RowInsertIntoTable(GetDataTableFromRawDataChunk([dataToSend]));
        return dataToSend.CloneDetailed();
    }
}
