using System.Reflection;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using BlushingPenguin.JsonPath;
using Elasticsearch.Net;
using Microsoft.Extensions.Logging;
using Nest;
using QaaS.Framework.Protocols.ConfigurationObjects.Elastic;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Session;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.Protocols.Protocols;

[ExcludeFromCodeCoverage]
public class ElasticProtocol : IChunkReader, IChunkSender, IDisposable
{
    private readonly IElasticClient _elasticClient;
    private readonly ILogger _logger;
    private readonly List<string> _activeScrollIds = [];
    private readonly ElasticReaderConfig _readerConfiguration = null!;
    private readonly ElasticSenderConfig _senderConfiguration = null!;
    private readonly ElasticIndicesRegex _elasticIndicesConfiguration = null!;
    private readonly DataFilter _dataFilter;


    /// <summary>
    /// Constructor that requires Elasticsearch reader configuration.
    /// </summary>
    public ElasticProtocol(ElasticReaderConfig configuration, DataFilter dataFilter, ILogger logger) : this(
        (ElasticIndicesRegex)configuration, dataFilter, logger)
    {
        _readerConfiguration = configuration;
    }

    /// <summary>
    /// Constructor that requires Elasticsearch any indices regex configuration.
    /// </summary>
    public ElasticProtocol(ElasticIndicesRegex configuration, DataFilter dataFilter, ILogger logger) : this(
        configuration, configuration.IndexPattern!, dataFilter, logger)
    {
        _elasticIndicesConfiguration = configuration;
    }

    /// <summary>
    /// Constructor that requires Elasticsearch sender configuration.
    /// </summary>
    public ElasticProtocol(ElasticSenderConfig configuration, DataFilter dataFilter, ILogger logger) : this(
        configuration, configuration.IndexName!, dataFilter, logger)
    {
        _senderConfiguration = configuration;
    }

    /// <summary>
    /// Constructor that requires Elasticsearch indices configuration <see cref="BaseElasticConfig"/> inherited object,
    /// for example - <inheritdoc cref="ElasticReaderConfig"/> or <inheritdoc cref="ElasticIndicesRegex"/>.
    /// </summary>
    public ElasticProtocol(BaseElasticConfig configuration, string indexName, DataFilter dataFilter,
        ILogger logger) : this(new ElasticClient(
            new ConnectionSettings(new Uri(configuration.Url!))
                .DefaultIndex(indexName)
                .BasicAuthentication(configuration.Username!, configuration.Password!)
                .RequestTimeout(TimeSpan.FromMilliseconds(configuration.RequestTimeoutMs))
                .ServerCertificateValidationCallback((_, _, _, _) => true)), // Ignores SSL certificate validation
        dataFilter, logger)
    {
    }

    /// <summary>
    /// Constructs protocol object using existing Elastic connection representative object.
    /// </summary>
    public ElasticProtocol(IElasticClient elasticClient, DataFilter dataFilter, ILogger logger)
    {
        _elasticClient = elasticClient;
        _dataFilter = dataFilter;
        _logger = logger;
    }

    public SerializationType? GetSerializationType() => SerializationType.Json;

    protected virtual DateTime GetCurrentDateTimeUtc() => DateTime.UtcNow;

    public IEnumerable<DetailedData<object>> ReadChunk(TimeSpan timeout)
    {
        var utcConsumptionStartTime = GetCurrentDateTimeUtc() -
                                      TimeSpan.FromSeconds(_readerConfiguration.FilterSecondsBeforeRunStartTime);
        WaitUntilConsumptionTimeoutIsReached(timeout);
        Func<SearchDescriptor<dynamic?>, ISearchRequest> elasticSelector = s =>
        {
            var descriptor = s
                .Scroll(new Time(TimeSpan.FromMilliseconds(_readerConfiguration.ScrollContextExpirationMs)))
                .Size(_readerConfiguration.ReadBatchSize)
                // Query items according to user match query
                .Query(q => q
                    .QueryString(qs => qs
                        .Query(_readerConfiguration.MatchQueryString)))
                // Arrange items from first to last
                .Sort(sort => sort
                    .Ascending(_readerConfiguration.TimestampField)
                );
            // If consumption start time is enabled, only query items that are newer than the start time
            if (_readerConfiguration.ReadFromRunStartTime)
                descriptor = descriptor.Query(q => q
                    .Bool(b => b
                        .Must(mu => mu
                                .DateRange(r => r
                                    .Field(_readerConfiguration.TimestampField)
                                    .GreaterThan(utcConsumptionStartTime)),
                            mu => mu
                                .QueryString(qs => qs
                                    .Query(_readerConfiguration.MatchQueryString)))));
            // If body is filtered only query the timestamp field from the elastic without the rest of the document
            if (!_dataFilter.Body)
                descriptor = descriptor.Source(src => src
                    .Includes(i => i
                        .Field(_readerConfiguration.TimestampField)));
            return descriptor;
        };
        return ScrollRead(() => _elasticClient.Search(elasticSelector))
            .Select(data => data.CastObjectDetailedData<object>());
    }

    public IEnumerable<DetailedData<object>> SendChunk(IEnumerable<Data<object>> chunkDataToSend)
    {
        var bulkDescriptor = new BulkDescriptor();
        var toPublish = chunkDataToSend.ToList();
        if (!toPublish.Any())
            return [];
        
        // Create bulk descriptor from data
        bulkDescriptor.IndexMany(toPublish.Select(data => data.Body).ToList());

        var bulkResponse = _elasticClient.Bulk(bulkDescriptor);
        var resultUtc = DateTime.UtcNow;
        if (bulkResponse.Errors || !bulkResponse.ApiCall.Success)
            throw new ElasticsearchClientException($"Failed to publish documents batch to elastic index" +
                                                   $" {_senderConfiguration.IndexName} \n {bulkResponse.DebugInformation}");
        _logger.LogDebug(
            "Successfully published batch containing {BatchSize} documents to elastic index {ElasticIndex}",
            toPublish.Count, _senderConfiguration.IndexName);
        return toPublish.Select(message => message.CloneDetailed(resultUtc));
    }

    public void Dispose()
    {
        _elasticClient.ClearScroll(cs => cs.ScrollId(_activeScrollIds.ToArray()));
    }

    /// <summary>
    /// Reads data from the elastic indices using the elastic `scroll` method for consuming large amounts of data
    /// </summary>
    /// <param name="scrollRequest"> The initial request to open the scroll against the elastic </param>
    private IEnumerable<DetailedData<object>> ScrollRead(Func<ISearchResponse<dynamic?>> scrollRequest)
    {
        var scrollResponse = scrollRequest.Invoke();
        _activeScrollIds.Add(scrollResponse.ScrollId);
        while (scrollResponse.Documents.Any())
        {
            foreach (var document in scrollResponse.Documents)
            {
                yield return new DetailedData<object>
                {
                    Body = JsonSerializer.SerializeToNode(document),
                    MetaData = null,
                    Timestamp = GetUtcTimeFromDocument(document) ??
                                throw new TargetException("Failed to get timestamp of queried data that matches " +
                                                          $"{_readerConfiguration.MatchQueryString} from index pattern {_readerConfiguration.IndexPattern}")
                };
            }

            scrollResponse = _elasticClient.Scroll<dynamic?>(
                new Time(TimeSpan.FromMilliseconds(_readerConfiguration.ScrollContextExpirationMs)),
                scrollResponse.ScrollId);
        }
    }

    /// <summary>
    /// Returns the number of milliseconds that have passed since the last insertion to the configured
    /// elasticsearch index pattern
    /// </summary>
    private long? GetNumberOfMilliSecondsPassedSinceLastInsertionToElasticIndexPattern()
    {
        var searchResponse = _elasticClient.Search<dynamic?>(s => s
            // Query items according to user match query
            .Query(q => q
                .QueryString(qs => qs
                    .Query(_readerConfiguration.MatchQueryString)))
            // Only take the timestamp field from the queried items
            .Source(src => src
                .Includes(i => i
                    .Field(_readerConfiguration.TimestampField)))
            // Only take the item with the latest timestamp field
            .Sort(sort => sort
                .Descending(_readerConfiguration.TimestampField)
            )
            .Size(1)
        );
        var latestTimeDocument = searchResponse.Documents.FirstOrDefault();
        var latestTime = GetUtcTimeFromDocument(latestTimeDocument) as DateTime?;
        if (searchResponse.IsValid && latestTime is not null)
        {
            if (latestTime.Value.Kind != DateTimeKind.Unspecified)
                return (long)Math.Round(
                    (GetCurrentDateTimeUtc() - latestTime.Value.ToUniversalTime()).TotalMilliseconds,
                    MidpointRounding.ToZero);

            // DateTimeKind not specified -> cannot convert to UTC and determine how many seconds passed
            _logger.LogCritical("Latest insertion to index pattern {IndexPattern} that matches " +
                                "{ElasticMatchQuery} is of `unspecified` date time" +
                                " kind so its impossible to know the timezone and determine how much" +
                                " time passed since the last insertion, timing out automatically",
                _readerConfiguration.IndexPattern!, _readerConfiguration.MatchQueryString);
            return null;
        }

        _logger.LogInformation("Latest insertion to index pattern {IndexPattern} that matches {ElasticMatchQuery}" +
                               " could not be found, timing out automatically. response debug information: {ResponseMessage}",
            _readerConfiguration.IndexPattern!, _readerConfiguration.MatchQueryString, searchResponse.DebugInformation);
        return null;
    }

    /// <summary>
    /// Waits until a period of time the length of the configured timeout has passed since the last insertion to
    /// the elasticsearch pattern
    /// </summary>
    private void WaitUntilConsumptionTimeoutIsReached(TimeSpan timeout)
    {
        long? milliSecondsSinceLastElasticIndexInsertion = 0;
        do
        {
            Thread.Sleep((int)(timeout.TotalMilliseconds - milliSecondsSinceLastElasticIndexInsertion));
            milliSecondsSinceLastElasticIndexInsertion =
                GetNumberOfMilliSecondsPassedSinceLastInsertionToElasticIndexPattern();
            if (milliSecondsSinceLastElasticIndexInsertion == null)
            {
                _logger.LogWarning("Encountered an issue when getting the number of milliseconds passed " +
                                   "since the last insertion to elastic index pattern {IndexPattern}" +
                                   " that matched {ElasticMatchQuery}" +
                                   ", setting amount of time passed since last change to the timeout",
                    _readerConfiguration.IndexPattern, _readerConfiguration.MatchQueryString);
                milliSecondsSinceLastElasticIndexInsertion = (int)timeout.TotalMilliseconds;
            }

            _logger.LogDebug("Since last insertion to elastic index pattern {IndexPattern} " +
                             "that matched {ElasticMatchQuery} {MilliSecondsSinceLastLastChange} milliseconds have passed," +
                             " timeout is {TimeoutMilliSeconds} milliseconds",
                _readerConfiguration.IndexPattern, _readerConfiguration.MatchQueryString,
                milliSecondsSinceLastElasticIndexInsertion, timeout.TotalMilliseconds);
        } while (milliSecondsSinceLastElasticIndexInsertion < timeout.TotalMilliseconds);
    }

    private DateTime? GetUtcTimeFromDocument(dynamic? document)
    {
        if (document == null) return null;
        var pathResults = ((JsonDocument?)JsonSerializer.SerializeToDocument(document))?
            .SelectToken(_readerConfiguration.TimestampField);
        var dateTimeValue = pathResults.GetValueOrDefault().GetDateTime();
        return pathResults is null ? null : dateTimeValue.ToUniversalTime();
    }

    public void EmptyElasticIndices()
    {
        // Get all relevant index names
        var indexNames = GetIndexNames(_readerConfiguration.IndexPattern!).ToArray();

        _logger.LogInformation("Found {NumberOfIndexes} indexes to empty with index pattern {EmptiedIndexPattern}",
            indexNames.Length, _readerConfiguration.IndexPattern!);

        Parallel.ForEach(indexNames, index =>
        {
            var response = _elasticClient.DeleteByQuery<object>(d => d
                .Index(index)
                .Query(q => q
                    .QueryString(qs => qs
                        .Query(_readerConfiguration.MatchQueryString)))
                .Conflicts(Conflicts.Proceed));
            if (response is not { IsValid: true } || response.ApiCall is not { Success: true })
                throw new Exception(
                    $"Failed to empty index {index} because {response}");

            _logger.LogDebug("Index {EmptiedIndex} emptied successfully",
                index);
        });
        _logger.LogInformation("All indexes under index pattern {EmptiedIndexPattern} emptied successfully",
            _readerConfiguration.IndexPattern!);
    }

    /// <summary>
    /// Returns all the indexes relevant to the configured index pattern
    /// </summary>
    protected virtual IEnumerable<string> GetIndexNames(string indexPattern)
    {
        return _elasticClient.Cat.Indices(
                new CatIndicesRequest(indexPattern))
            .Records
            .Select(record => record.Index);
    }

    public void Connect()
    {
    }

    public void Disconnect()
    {
    }
}
