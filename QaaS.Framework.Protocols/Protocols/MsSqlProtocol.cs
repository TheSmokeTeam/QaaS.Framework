using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;

namespace QaaS.Framework.Protocols.Protocols;

public class MsSqlProtocol : BaseSqlProtocol<SqlConnection>
{
    public MsSqlProtocol(MsSqlReaderConfig configurations, ILogger logger,
        SqlConnection? dbConnection = null) : base(configurations, logger,
        dbConnection ?? new SqlConnection(configurations.ConnectionString))
    {
    }

    public MsSqlProtocol(MsSqlSenderConfig configurations, ILogger logger,
        SqlConnection? dbConnection = null) : base(configurations, logger,
        dbConnection ?? new SqlConnection(configurations.ConnectionString))
    {
    }

    protected override void InsertChunkToTable(DataTable chunkData)
    {
        using var bulkCopy = new SqlBulkCopy(DbConnection);

        bulkCopy.BulkCopyTimeout = CommandTimeoutSeconds;
        bulkCopy.DestinationTableName = TableName;
        bulkCopy.BatchSize = chunkData.Rows.Count;

        // Map the columns automatically
        foreach (DataColumn col in chunkData.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        bulkCopy.WriteToServer(chunkData);
    }

    /// <inheritdoc />
    protected override string GetTableQueryArrangedByInsertionTimeFieldAsc() =>
        $"select * from {TableName} {BuildWhereStatement()} order by {InsertionTimeField} asc";

    /// <inheritdoc />
    protected override string GetTableQueryWithoutRegardToInsertionTimeField() =>
        $"select * from {TableName} {BuildWhereStatement()}";

    /// <inheritdoc />
    protected override string GetLatestTableRowQuery() =>
        $"select top 1 * from {TableName} {BuildWhereStatement()} order by {InsertionTimeField} desc";

    protected override string GetTimeFieldSqlFormat(DateTime time) => $"{time:yyyy-MM-dd HH:mm:ss.fff}";

    private string BuildWhereStatement()
    {
        if (WhereStatement == null)
            return FilterFromStartTime
                ? $"where {InsertionTimeField} > '{GetTimeFieldSqlFormat(StartTimeDbTimeZone!.Value)}'"
                : string.Empty;
        return FilterFromStartTime
            ? $"where ({InsertionTimeField} > '{GetTimeFieldSqlFormat(StartTimeDbTimeZone!.Value)}') and ({WhereStatement})"
            : $"where {WhereStatement}";
    }
}