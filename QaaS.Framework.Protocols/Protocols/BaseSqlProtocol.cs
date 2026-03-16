using System.Collections.Immutable;
using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Infrastructure;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.Protocols.Protocols;

public abstract class BaseSqlProtocol<TDbConnection> : IChunkReader, IChunkSender
    where TDbConnection : class, IDbConnection
{
    private readonly int _insertionTimeTimeZoneOffsetSummerTime;
    private readonly double _filterSecondsBeforeRunStartTime;
    private protected readonly string TableName;
    protected readonly ILogger Logger;
    protected readonly int CommandTimeoutSeconds;
    protected readonly string[]? ColumnsToFilter;

    protected TDbConnection DbConnection = null!;
    protected string? InsertionTimeField { get; set; }
    public DateTime? StartTimeDbTimeZone { get; set; }
    protected virtual DateTime GetCurrentUtcDateTime() => DateTime.UtcNow;
    protected string? WhereStatement { get; set; }

    protected bool FilterFromStartTime { get; set; }


    protected BaseSqlProtocol(SqlReaderConfig configurations, ILogger logger,
        TDbConnection? dbConnection = null) : this((SqlConfig)configurations, logger, dbConnection)
    {
        _insertionTimeTimeZoneOffsetSummerTime = configurations.InsertionTimeTimeZoneOffsetSummerTime;
        ColumnsToFilter = configurations.ColumnsToIgnore;
        _filterSecondsBeforeRunStartTime = configurations.FilterSecondsBeforeRunStartTime;
        WhereStatement = configurations.WhereStatement;
        FilterFromStartTime = configurations.ReadFromRunStartTime;
        InsertionTimeField = configurations.InsertionTimeField;
    }


    protected BaseSqlProtocol(SqlConfig configurations, ILogger logger,
        TDbConnection? dbConnection = null)
    {
        if (dbConnection != null) DbConnection = dbConnection;
        CommandTimeoutSeconds = configurations.CommandTimeoutSeconds;
        TableName = configurations.TableName!;
        Logger = logger;
    }

    public SerializationType? GetSerializationType() => SerializationType.Json;

    public IEnumerable<DetailedData<object>> ReadChunk(TimeSpan timeout)
    {
        WaitUntilReadTimeoutIsReached(timeout);
        var rowsDetailedDataEnumerable = GetJsonEnumerableFromQuery(
                InsertionTimeField != null
                    ? GetTableQueryArrangedByInsertionTimeFieldAsc()
                    : GetTableQueryWithoutRegardToInsertionTimeField(), CommandTimeoutSeconds, ColumnsToFilter)
            .Select(row => new DetailedData<object>
            {
                Body = row,
                MetaData = null,
                Timestamp = GetDateTimeFromDateTimeField(row)?
                    .ConvertDateTimeToUtcByTimeZoneOffset(_insertionTimeTimeZoneOffsetSummerTime)
            });
        return rowsDetailedDataEnumerable.ToImmutableList();
    }


    public virtual IEnumerable<DetailedData<object>> SendChunk(IEnumerable<Data<object>> chunkDataToSend)
    {
        var dataToSend = chunkDataToSend.ToList();
        InsertChunkToTable(GetDataTableFromRawDataChunk(dataToSend));
        var chunkInsertionTime = DateTime.UtcNow;
        Logger.LogDebug("Finished sending chunk");
        return dataToSend.Select(message => message.CloneDetailed(chunkInsertionTime)).ToImmutableList();
    }

    public virtual void Connect()
    {
        DbConnection.Open();
        StartTimeDbTimeZone = GetCurrentUtcDateTime()
                                  .ConvertDateTimeFromUtcToTimeZoneByTimeZoneOffset(
                                      _insertionTimeTimeZoneOffsetSummerTime)
                              - TimeSpan.FromSeconds(_filterSecondsBeforeRunStartTime);
    }

    public virtual void Disconnect()
    {
        DbConnection.Close();
        DbConnection.Dispose();
    }

    /// <summary>
    /// Query an sql table and save the result in a json enumerable where every item is a row in the table
    /// </summary>
    /// <param name="queryCommand"> The query to execute to get the table </param>
    /// <param name="commandTimeoutSeconds">The wait time (in seconds) before terminating the attempt to
    /// execute a read sql command and generating an error</param>
    /// <param name="columnsToIgnore">The columns to ignore in the sql query results, if no columns are given
    /// doesn't ignore any columns</param>
    /// <returns>The result of the query </returns>
    protected IEnumerable<JsonObject> GetJsonEnumerableFromQuery(string queryCommand, int commandTimeoutSeconds = 30,
        string[]? columnsToIgnore = null)
    {
        columnsToIgnore ??= [];
        using var command = CreateDbCommand();
        command.CommandText = queryCommand;
        command.CommandTimeout = commandTimeoutSeconds;
        if (DbConnection.State == ConnectionState.Closed)
            DbConnection.Open();
        using (var reader = command.ExecuteReader()) // Query trigger
        {
            while (reader.Read()) // Results rows reader
            {
                var row = new JsonObject();
                for (var col = 0; col < reader.FieldCount; col++)
                {
                    var columnName = reader.GetName(col);
                    if (columnsToIgnore.Contains(columnName)) continue;

                    var value = GetValueFromReader(reader, col);
                    row[columnName] = value == DBNull.Value ? null : JsonValue.Create(value);
                }

                yield return row;
            }
        }

        if (DbConnection.State != ConnectionState.Closed)
            DbConnection.Close();
    }

    /// <summary>
    /// Inserts the datatable to the database using INSERT INTO command and executes it one by one.
    /// </summary>
    protected virtual void RowInsertIntoTable(DataTable chunkDataToSend)
    {
        using var sqlCommand = DbConnection.CreateCommand();
        sqlCommand.CommandTimeout = CommandTimeoutSeconds;

        foreach (DataRow row in chunkDataToSend.Rows)
        {
            // Build columns names and values as strings for INSERT INTO query VALUES
            var columnNames = new StringBuilder();
            var columnValues = new StringBuilder();
            foreach (DataColumn col in chunkDataToSend.Columns)
            {
                columnNames.Append($"{col.ColumnName}, ");
                columnValues.Append($"{GetParsedSqlParameter(row[col])}, ");
            }

            var columnNamesAsString = columnNames.ToString().TrimEnd(',', ' ');
            var columnValuesAsString = columnValues.ToString().TrimEnd(',', ' ');

            var insertCommandText =
                $"INSERT INTO {TableName} ({columnNamesAsString}) VALUES ({columnValuesAsString})";
            sqlCommand.CommandText = insertCommandText;
            sqlCommand.ExecuteNonQuery();
        }
    }


    /// <summary>
    /// Inserts a chunk of data to the desired SQL table
    /// </summary>
    protected abstract void InsertChunkToTable(DataTable chunkData);

    /// <summary>
    /// Creates the db command object used to execute the query on the db
    /// </summary>
    protected virtual IDbCommand CreateDbCommand() => DbConnection.CreateCommand();

    /// <summary>
    /// Returns a string of an sql query to get the table by from user configurations by the insertion time field asc
    /// </summary>
    protected abstract string GetTableQueryArrangedByInsertionTimeFieldAsc();

    /// <summary>
    /// Returns a string of an sql query to get the table by from user configurations without using the insertion time field
    /// </summary>
    protected abstract string GetTableQueryWithoutRegardToInsertionTimeField();

    /// <summary>
    /// Returns a string of an sql query to get the most recently changed/inserted row in the table
    /// </summary>
    protected abstract string GetLatestTableRowQuery();

    /// <summary>
    /// A function that gets a value from a specific columns from a db data reader as a generic object
    /// </summary>
    /// <param name="reader"> The db reader </param>
    /// <param name="col"> The column to get the value from </param>
    /// <returns> The value in the column as an object </returns>
    private object GetValueFromReader(IDataRecord reader, int col)
    {
        object value;
        try
        {
            value = reader.GetValue(col);
        }
        catch (Exception e) when (e is InvalidOperationException or InvalidCastException)
        {
            // Taking care of reading a UDT value (user-defined type)
            Logger.LogDebug("Encountered a problem reading value from column {ColumnIndex}," +
                            " getting its value as string", col);
            value = reader.GetString(col);
        }

        return value;
    }

    /// <summary>
    /// Waits until a period of time the length of the configured timeout has passed since the last message
    /// received in the database 
    /// </summary>
    private void WaitUntilReadTimeoutIsReached(TimeSpan timeout)
    {
        long? milliSecondsSinceLastTableChange = 0;
        do
        {
            Thread.Sleep((int)timeout.TotalMilliseconds - (int)milliSecondsSinceLastTableChange);
            milliSecondsSinceLastTableChange = GetNumberOfMilliSecondsPassedSinceLastTableChange();
            if (milliSecondsSinceLastTableChange == null)
            {
                Logger.LogWarning("Encountered an issue when getting the number of milliseconds passed " +
                                  "since the last table change in table {TableName}" +
                                  ", setting amount of time passed since last table change to the timeout",
                    TableName);
                milliSecondsSinceLastTableChange = (long)timeout.TotalMilliseconds;
            }
        } while (milliSecondsSinceLastTableChange < (long)timeout.TotalMilliseconds);

        Logger.LogDebug("Read timeout reached for table {TableName} after {MilliSecondsSinceLastTableChange} milliseconds since the latest change",
            TableName, milliSecondsSinceLastTableChange);
    }

    /// <summary>
    /// Returns the number of milliseconds that have passed since the last table change, if no table change was ever
    /// made or the latest table change time could not be found return null
    /// </summary>
    protected virtual long? GetNumberOfMilliSecondsPassedSinceLastTableChange()
    {
        var latestTableRowQuery = GetLatestTableRowQuery();
        if (InsertionTimeField == null)
        {
            Logger.LogWarning("No {InsertionTimeFieldField} was configured so timeout is constant and" +
                              " not since latest change in the table {TableName}",
                nameof(InsertionTimeField), TableName);
            return null;
        }

        try
        {
            var latestChangeTime = GetDateTimeFromDateTimeField(
                GetJsonEnumerableFromQuery(latestTableRowQuery).FirstOrDefault())!.Value;
            Logger.LogTrace("Executed Query {LatestTableRowQuery} to get the time of the latest change to the" +
                            " table {TableName}, Query result is {LatestChangeTime}",
                latestTableRowQuery, TableName, latestChangeTime);

            return (long)(GetCurrentUtcDateTime() -
                          latestChangeTime.ConvertDateTimeToUtcByTimeZoneOffset
                              (_insertionTimeTimeZoneOffsetSummerTime)).TotalMilliseconds;
        }
        catch (InvalidOperationException e)
        {
            Logger.LogWarning("Encountered exception {ExceptionMessage}," +
                              " when searching for the time of the latest change in the table {TableName}",
                e.Message, TableName);
            return null;
        }
    }

    /// <summary>
    /// Gets a datetime object from the date time field in a row of the table (represented as json)
    /// </summary>
    /// <param name="row"> The row to get the datetime from </param>
    /// <returns> A datetime object with the same date as the datetime field in the given row </returns>
    /// <exception cref="InvalidOperationException"> Raises exception if it could not parse the
    /// insertion time field to datetime or could not find the insertion time field value </exception>
    protected DateTime? GetDateTimeFromDateTimeField(JsonObject? row)
    {
        if (InsertionTimeField == null)
        {
            Logger.LogWarning("No {InsertionTimeFieldField} was configured so date time cannot be given to" +
                              " queried data from the table {TableName}",
                nameof(InsertionTimeField), TableName);
            return null;
        }

        if (row?[InsertionTimeField] == null)
            throw new InvalidOperationException(
                $"Could not find the insertion time field - {InsertionTimeField} with a value");

        return row[InsertionTimeField]?.GetValue<DateTime>() ?? throw new InvalidOperationException(
            "Could not parse insertion time field to datetime");
    }

    protected static DataTable GetDataTableFromRawDataChunk(IEnumerable<Data<object>> chunkData)
    {
        var dataTable = new DataTable();
        foreach (var itemToSend in chunkData)
        {
            // Check if given data item is `JsonObject` already, if not try to convert it to `JsonObject`
            JsonObject? json;
            if (itemToSend.Body?.GetType() == typeof(JsonObject))
                json = itemToSend.Body as JsonObject;
            else
                json = itemToSend.Body == null
                    ? null
                    : JsonNode.Parse(JsonSerializer.Serialize(itemToSend.Body))?.AsObject();

            if (json == null)
                throw new ArgumentException("Can't send null json to sql table");
            var row = dataTable.NewRow();
            foreach (var property in json.AsEnumerable())
            {
                var propertyValue = property.Value?.GetValue<object?>();

                // Add column metadata to columns
                if (!dataTable.Columns.Contains(property.Key))
                    dataTable.Columns.Add(propertyValue != null
                        ? new DataColumn(property.Key, propertyValue.GetType())
                        : new DataColumn(property.Key, typeof(object)));

                // If column has default type and add it again with correct type
                else if (dataTable.Columns[property.Key]?.DataType == typeof(object) && propertyValue != null)
                {
                    dataTable.Columns.Remove(property.Key);
                    dataTable.Columns.Add(new DataColumn(property.Key, propertyValue.GetType()));
                }

                row[property.Key] = propertyValue ?? DBNull.Value;
            }

            dataTable.Rows.Add(row);
        }

        return dataTable;
    }

    /// <summary>
    /// Parse string by data type to suit Sql query
    /// </summary>
    /// <param name="value">Value to parse</param>
    /// <returns>Parsed value</returns>
    private object? GetParsedSqlParameter(object? value)
    {
        const string udtRegexPattern = @"^\b\w+\b\((.*)\)";
        if (value == null || value == DBNull.Value)
            return "NULL";
        if (DateTime.TryParse(value.ToString(), out var time))
            return GetTimeFieldSqlFormat(time);
        var regex = new Regex(udtRegexPattern);
        if (regex.IsMatch(value.ToString()!)) 
            return value;
        return $"'{value}'";
    }

    /// <summary>
    /// Parses the datetime value to suit sql query
    /// </summary>
    /// <param name="time"> Generated datetime value to send </param>
    /// <returns> The datetime as string to suit sql query </returns>
    protected abstract string GetTimeFieldSqlFormat(DateTime time);
}
