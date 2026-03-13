using System.Text.Json;
using System.Text.Json.Nodes;
using QaaS.Framework.SDK.Session.DataObjects;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Protocols.ConfigurationObjects.Prometheus;
using QaaS.Framework.Serialization;


namespace QaaS.Framework.Protocols.Protocols;

public class PrometheusProtocol : IFetcher
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    private readonly PrometheusFetcherConfig _fetcherConfig;
    private readonly ILogger _logger;


    // Request URL building constants
    private const string QueryRangeApiTemplate =
        "{0}/api/v1/query_range?query={1}&start={2}&end={3}&step={4}ms&timeout={5}ms";

    // Response content parsing constants
    private const string MatrixResultType = "matrix", SuccessfulStatus = "success";

    // Body building constants
    private const string VectorValueArrayJsonKey = "value", VectorMetricJsonKey = "metric";

    public PrometheusProtocol(PrometheusFetcherConfig fetcherConfig, ILogger logger)
    {
        _fetcherConfig = fetcherConfig;
        _logger = logger;
    }


    public IEnumerable<DetailedData<object>> Collect(DateTime collectionStartTimeUtc,
        DateTime collectionEndTimeUtc)
    {
        var queryRequestUri = string.Format(QueryRangeApiTemplate, _fetcherConfig.Url,
            Uri.EscapeDataString(_fetcherConfig.Expression!), collectionStartTimeUtc.ToString("o"),
            collectionEndTimeUtc.ToString("o"), _fetcherConfig.SampleIntervalMs, _fetcherConfig.TimeoutMs);
        var body = HttpGetResultBodyAsString(queryRequestUri);
        MatrixResult matrixResult;
        try
        {
            matrixResult = JsonSerializer.Deserialize<MatrixResult>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new ArgumentException(
                "Prometheus query range API response could nt be deserialized, received null when trying to deserialize it");
        }
        catch (Exception)
        {
            _logger.LogCritical("Failed to deserialize prometheus query range API result");
            throw;
        }

        var resultStatus = matrixResult.Status;
        if (resultStatus != SuccessfulStatus)
            throw new Exception($"Received the query status `{resultStatus}`" +
                                $" when executing the query `{_fetcherConfig.Expression}` " +
                                $"on prometheus query_range API.\n {body}.");

        return ParseResult(matrixResult).OrderBy(data => data.Timestamp);
    }

    public SerializationType? GetSerializationType() => SerializationType.Json;

    /// <summary>
    /// Performs an http get request on the given request URI and returns the result's body as a string
    /// </summary>
    protected virtual string HttpGetResultBodyAsString(string queryRequestUri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, queryRequestUri);
        using var timeoutCancellationTokenSource =
            new CancellationTokenSource(TimeSpan.FromMilliseconds(_fetcherConfig.TimeoutMs));

        if (!string.IsNullOrWhiteSpace(_fetcherConfig.ApiKey))
            request.Headers.Add("apikey", _fetcherConfig.ApiKey);

        using var response = SharedHttpClient.SendAsync(request, timeoutCancellationTokenSource.Token)
            .GetAwaiter()
            .GetResult();
        _logger.LogDebug("Received response from prometheus query_range API - {HttpResponse}",
            response.ToString());
        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Http request status code is not successful," +
                                           $" its `{response.StatusCode}` and the returned content is `{body}` ");
        return body;
    }

    /// <summary>
    /// Parses the following result that is given as a matrix -
    /// "result": [{"metric": {...}, "values": [ [unix_timestamp,"sample_value"], ...]}, ...]
    /// </summary>
    private static IEnumerable<DetailedData<object>> ParseResult(MatrixResult matrixResult)
    {
        var resultType = matrixResult.Data.ResultType;

        if (resultType != MatrixResultType)
            throw new ArgumentOutOfRangeException(nameof(matrixResult.Data.ResultType), resultType,
                $"Result type not supported - query_range API only support {MatrixResultType} result type");

        foreach (var result in matrixResult.Data.Result)
        {
            var metricLabels = result.Metric;

            foreach (var valueArray in result.Values)
            {
                if (!long.TryParse(valueArray[0].ToString(), out var valueTimeEpoch))
                    throw new ArgumentException(
                        "Could not parse first item of value array in prometheus matrix response values to " +
                        "type `long`, this field represents the Timestamp of the prometheus value and without it " +
                        "the prometheus values cannot be returned!");

                var timestamp = ConvertJTokenEpochUtcToDateTime(valueTimeEpoch);
                yield return new DetailedData<object>
                {
                    Body = new JsonObject
                    {
                        { VectorMetricJsonKey, JsonValue.Create(metricLabels) },
                        { VectorValueArrayJsonKey, JsonValue.Create(valueArray[1]) }
                    },
                    Timestamp = timestamp
                };
            }
        }
    }

    private static DateTime? ConvertJTokenEpochUtcToDateTime(JToken? epochTime)
    {
        return epochTime is not null ? DateTimeOffset.FromUnixTimeSeconds((long)epochTime).DateTime : null;
    }
}
