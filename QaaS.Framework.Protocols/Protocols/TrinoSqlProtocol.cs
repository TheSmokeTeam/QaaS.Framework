using System.Data;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.Protocols.Utils;
using Trino.Client.Auth;
using Trino.Data.ADO.Server;

namespace QaaS.Framework.Protocols.Protocols;

public class TrinoSqlProtocol : BaseSqlProtocol<TrinoConnection>
{
    private readonly long? _readBufferSizeBytes;
    public string Schema { get; set; }

    public TrinoSqlProtocol(
        TrinoReaderConfig configurations,
        ILogger logger,
        TrinoConnection? dbConnection = null,
        string? timeZoneId = null
    )
        : base(configurations, logger, dbConnection, timeZoneId)
    {
        _readBufferSizeBytes = configurations.ReadBufferSizeBytes;
        Schema = configurations.Schema!;
        if (dbConnection != null)
            return;
        var properties = new TrinoConnectionProperties
        {
            Catalog = configurations.Catalog,
            Server = new Uri(configurations.Hostname!),
            ClientTags = [configurations.ClientTag!],
            Auth = new LDAPAuth
            {
                User = configurations.Username,
                Password = configurations.Password,
            },
        };
        DbConnection = new TrinoConnection(properties);
        if (configurations.ConnectionString is { Length: > 0 })
            DbConnection.ConnectionString = configurations.ConnectionString;
    }

    protected override IDbCommand CreateDbCommand() => new TrinoTimeoutCommand(DbConnection);

    protected override IDataReader ExecuteReader(IDbCommand command) =>
        _readBufferSizeBytes.HasValue && command is TrinoCommand trino
            ? trino.ExecuteReader(_readBufferSizeBytes.Value)
            : base.ExecuteReader(command);

    protected override void InsertChunkToTable(DataTable chunkData)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    protected override string GetTableQueryArrangedByInsertionTimeFieldAsc() =>
        $"select {ReadProjection} from {Schema}.{TableName} {BuildWhereStatement()} order by {InsertionTimeField} asc";

    /// <inheritdoc />
    protected override string GetTableQueryWithoutRegardToInsertionTimeField() =>
        $"select {ReadProjection} from {Schema}.{TableName} {BuildWhereStatement()}";

    /// <inheritdoc />
    protected override string GetLatestTableRowQuery() =>
        $"select * from {Schema}.{TableName} {BuildWhereStatement()} order by {InsertionTimeField} desc limit 1";

    private string BuildWhereStatement()
    {
        if (WhereStatement == null)
            return FilterFromStartTime
                ? $"where {InsertionTimeField} > {GetTimeFieldSqlFormat(StartTimeDbTimeZone!.Value)}"
                : string.Empty;
        return FilterFromStartTime
            ? $"where ({InsertionTimeField} > {GetTimeFieldSqlFormat(StartTimeDbTimeZone!.Value)}) and ({WhereStatement})"
            : $"where {WhereStatement}";
    }

    protected override string GetTimeFieldSqlFormat(DateTime time) =>
        $"FROM_ISO8601_TIMESTAMP('{time:yyyy-MM-ddTHH:mm:ss.ffZ}')";
}
