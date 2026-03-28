using System.Data;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Framework.Protocols.Protocols;

[ExcludeFromCodeCoverage]
public class OracleSqlProtocol : BaseSqlProtocol<OracleConnection>, ISender
{
    public OracleSqlProtocol(OracleReaderConfig configurations, ILogger logger,
        OracleConnection? dbConnection = null,
        string? timeZoneId = null) : base(configurations, logger,
        dbConnection ?? new OracleConnection(configurations.ConnectionString), timeZoneId)
    {
    }

    public OracleSqlProtocol(OracleSenderConfig configurations, ILogger logger,
        OracleConnection? dbConnection = null,
        string? timeZoneId = null) : base(configurations, logger,
        dbConnection ?? new OracleConnection(configurations.ConnectionString), timeZoneId)
    {
    }

    protected override void InsertChunkToTable(DataTable chunkData)
    {
        using var bulkCopy = new OracleBulkCopy(DbConnection);

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

    public DetailedData<object> Send(Data<object> dataToSend)
    {
        RowInsertIntoTable(GetDataTableFromRawDataChunk([dataToSend]));
        return dataToSend.CloneDetailed();
    }


    /// <inheritdoc />
    protected override string GetTableQueryArrangedByInsertionTimeFieldAsc() =>
        $"select * from {TableName} {BuildWhereStatement()} order by {InsertionTimeField} asc";

    /// <inheritdoc />
    protected override string GetTableQueryWithoutRegardToInsertionTimeField() =>
        $"select * from {TableName} {BuildWhereStatement()}";

    /// <inheritdoc />
    protected override string GetLatestTableRowQuery() =>
        $"select * from (select * from {TableName} {BuildWhereStatement()} order by {InsertionTimeField} desc) where ROWNUM <= 1";

    protected override string GetTimeFieldSqlFormat(DateTime time) =>
        $"TO_DATE('{time:dd-MM-yyyy HH:mm:ss}', '{"DD/MM/YYYY hh24:mi:ss"}')";

    private string BuildWhereStatement()
    {
        if (WhereStatement == null)
            return FilterFromStartTime
                ? $"where {InsertionTimeField} > TO_TIMESTAMP('{StartTimeDbTimeZone:yyyy-MM-dd HH:mm:ss.fff}', 'YYYY-MM-DD HH24:MI:SS.FF3')"
                : string.Empty;
        return FilterFromStartTime
            ? $"where ({InsertionTimeField} > TO_TIMESTAMP('{StartTimeDbTimeZone:yyyy-MM-dd HH:mm:ss.fff}', 'YYYY-MM-DD HH24:MI:SS.FF3')) and ({WhereStatement})"
            : $"where {WhereStatement}";
    }
}

