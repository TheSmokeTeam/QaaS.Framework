using System.Data;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Npgsql;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Framework.Protocols.Protocols;

[ExcludeFromCodeCoverage]
public class PostgreSqlProtocol : BaseSqlProtocol<NpgsqlConnection>, ISender
{
    private readonly bool _isInsertionTimeFieldTimeZoneTz;

    public PostgreSqlProtocol(PostgreSqlReaderConfig configurations, ILogger logger,
        NpgsqlConnection? dbConnection = null) : base(configurations, logger,
        dbConnection ?? new NpgsqlConnection(configurations.ConnectionString))
    {
        _isInsertionTimeFieldTimeZoneTz = configurations.IsInsertionTimeFieldTimeZoneTz;
    }

    public PostgreSqlProtocol(PostgreSqlSenderConfig configurations, ILogger logger,
        NpgsqlConnection? dbConnection = null) : base(configurations,
        logger, dbConnection ?? new NpgsqlConnection(configurations.ConnectionString))
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

            var rowString = rowBuilder.ToString();
            Logger.LogDebug("Inserting row {RowString} to postgresql table {TableName}", rowString, TableName);
            writer.WriteLine(rowString);
        }

        writer.Close();
    }

    public override void Connect()
    {
        base.Connect();
        using var cmd = new NpgsqlCommand($"SET statement_timeout = {CommandTimeoutSeconds * 1000}",
            (NpgsqlConnection?)DbConnection);
        cmd.ExecuteNonQuery();
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

    public DetailedData<object> Send(Data<object> dataToSend)
    {
        RowInsertIntoTable(GetDataTableFromRawDataChunk([dataToSend]));
        return dataToSend.CloneDetailed();
    }
}
