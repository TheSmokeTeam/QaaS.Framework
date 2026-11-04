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
    protected override string SqlParameterPrefix => ":";

    public OracleSqlProtocol(
        OracleReaderConfig configurations,
        ILogger logger,
        OracleConnection? dbConnection = null,
        string? timeZoneId = null
    )
        : base(
            configurations,
            logger,
            dbConnection ?? new OracleConnection(configurations.ConnectionString),
            timeZoneId
        ) { }

    public OracleSqlProtocol(
        OracleSenderConfig configurations,
        ILogger logger,
        OracleConnection? dbConnection = null,
        string? timeZoneId = null
    )
        : base(
            configurations,
            logger,
            dbConnection ?? new OracleConnection(configurations.ConnectionString),
            timeZoneId
        ) { }

    protected override IDbDataParameter CreateSqlParameter(
        IDbCommand command,
        string name,
        object? value
    )
    {
        var parameter = base.CreateSqlParameter(command, name, value);
        if (
            parameter is OracleParameter oracle
            && parameter.Value is string text
            && System.Text.Encoding.UTF8.GetByteCount(text) > 4000
        )
            oracle.OracleDbType = OracleDbType.Clob;
        return parameter;
    }

    protected override string GetSqlParameterExpression(IDbDataParameter parameter, string? sqlType)
    {
        var lob = sqlType?.Trim().ToUpperInvariant() switch
        {
            "CLOB" => OracleDbType.Clob,
            "NCLOB" => OracleDbType.NClob,
            "BLOB" => OracleDbType.Blob,
            _ => (OracleDbType?)null,
        };
        if (lob.HasValue && parameter is OracleParameter oracle)
        {
            oracle.OracleDbType = lob.Value;
            // JSON represents byte arrays as base64 strings. Native byte[] values remain unchanged.
            if (lob == OracleDbType.Blob && oracle.Value is string encoded)
                oracle.Value = Convert.FromBase64String(encoded);
            return $"{SqlParameterPrefix}{parameter.ParameterName}";
        }
        return base.GetSqlParameterExpression(parameter, sqlType);
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
        $"select {ReadProjection} from {TableName} {BuildWhereStatement()} order by {InsertionTimeField} asc";

    /// <inheritdoc />
    protected override string GetTableQueryWithoutRegardToInsertionTimeField() =>
        $"select {ReadProjection} from {TableName} {BuildWhereStatement()}";

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
