using System.Net.Http.Headers;
using JWT.Builder;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Protocols.ConfigurationObjects.Http;
using QaaS.Framework.Protocols.Extentions;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.Protocols.Protocols;

public class HttpProtocol : ITransactor, IDisposable
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly HttpTransactorConfig _transactorConfiguration;
    private bool _disposed;
    public HttpMethods Method;

    public HttpProtocol(HttpTransactorConfig configuration, ILogger logger, TimeSpan timeout)
    {
        _logger = logger;
        Method = configuration.Method;
        _transactorConfiguration = configuration;
        var baseAddress = configuration.BaseAddress!.EndsWith('/')
            ? configuration.BaseAddress.Remove(configuration.BaseAddress.Length - 1)
            : configuration.BaseAddress;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(timeout.TotalMilliseconds),
            BaseAddress = configuration.Port != null
                ? new Uri($"{baseAddress}:{configuration.Port}")
                : new Uri(baseAddress)
        };

        if (configuration.JwtAuth != null)
            AddJwtAuthByConfig(configuration.JwtAuth);
    }

    /// <summary>
    /// Generating and adding a JWT as a Bearer authorization header 
    /// </summary>
    private void AddJwtAuthByConfig(JwtAuthConfig jwtAuthConfig)
    {
        if (!jwtAuthConfig.BuildJwtConfig)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(jwtAuthConfig.HttpAuthScheme.ToString(), jwtAuthConfig.Secret);
            return;
        }

        var builder = JwtBuilder.Create()
            .WithAlgorithm(HttpExtentions.GetJwtAlgorithmFromJwtEnum(jwtAuthConfig.JwtAlgorithm))
            .WithSecret(jwtAuthConfig.Secret!);
        if (jwtAuthConfig.HierarchicalClaims == null)
        {
            foreach (var claim in jwtAuthConfig.Claims)
            {
                builder.AddClaim(claim.Key, claim.Value);
            }
        }
        else
        {
            var claims = HttpExtentions.GetClaimsFromHierarchicalClaims(jwtAuthConfig.HierarchicalClaims);
            foreach (var claim in claims)
            {
                builder.AddClaim(claim.Key, claim.Value);
            }
        }

        var token = builder.Encode();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(jwtAuthConfig.HttpAuthScheme.ToString(), token);
    }

    public Tuple<DetailedData<object>, DetailedData<object>?> Transact(Data<object> dataToSend)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var requestUri = $"{_httpClient.BaseAddress}{_transactorConfiguration.Route}";
        _logger.LogDebug("Started sending http requests to: {RequestUri}",
            requestUri);
        var result = InvokeHttpRequest(dataToSend.CastObjectData<byte[]>(), requestUri);
        return new Tuple<DetailedData<object>, DetailedData<object>?>(dataToSend.CloneDetailed(result.Key),
            result.Value?.CastToObjectDetailedData());
    }

    public SerializationType? GetInputCommunicationSerializationType() => null;

    public SerializationType? GetOutputCommunicationSerializationType() => null;

    public void Dispose()
    {
        if (_disposed)
            return;

        _httpClient.Dispose();
        _disposed = true;
    }

    private KeyValuePair<DateTime, DetailedData<byte[]>?> InvokeHttpRequest(Data<byte[]> data, string requestUri)
    {
        var requestUtcTime = DateTime.UtcNow;
        for (var attempt = 1; attempt <= _transactorConfiguration.Retries; attempt++)
        {
            using var requestData = CreateRequest(data, requestUri);
            try
            {
                using var responseData = _httpClient.Send(requestData);
                var responseUtcTime = DateTime.UtcNow;

                return new KeyValuePair<DateTime, DetailedData<byte[]>?>(
                    requestUtcTime,
                    new DetailedData<byte[]>
                    {
                        Body = responseData.Content.ReadAsByteArrayAsync().ConfigureAwait(false).GetAwaiter().GetResult(),
                        MetaData = new MetaData
                        {
                            Http = new Http
                            {
                                StatusCode = (int?)responseData.StatusCode,
                                ReasonPhrase = responseData.ReasonPhrase,
                                Version = responseData.Version.ToString(),
                                Headers = responseData.Content.Headers.ToDictionary(header =>
                                    header.Key, header => string.Join(",", header.Value)),
                                ResponseHeaders = responseData.Headers.ToDictionary(header =>
                                    header.Key, header => string.Join(",", header.Value)),
                                TrailingHeaders = responseData.TrailingHeaders.ToDictionary(header =>
                                    header.Key, header => string.Join(",", header.Value))
                            }
                        },
                        Timestamp = responseUtcTime
                    });
            }
            catch (TaskCanceledException transactException) when (attempt < _transactorConfiguration.Retries)
            {
                _logger.LogWarning(
                    "Timeout exceeded when performing an {TransactionType} - {HttpMethod} call, retrying" +
                    " {Retries}/{ConfiguredRetries} times. {Exception}",
                    typeof(HttpProtocol), Method, attempt, _transactorConfiguration.Retries, transactException);
            }
            catch (HttpRequestException transactException) when (attempt < _transactorConfiguration.Retries)
            {
                _logger.LogWarning(
                    "Received exception when performing an {TransactionType} - {HttpMethod} call - {Exception}, retrying" +
                    " {Retries}/{ConfiguredRetries} times", typeof(HttpProtocol), Method, transactException, attempt,
                    _transactorConfiguration.Retries);
            }
            catch (TaskCanceledException transactException)
            {
                _logger.LogWarning(
                    "Timeout exceeded when performing an {TransactionType} - {HttpMethod} call. {Exception}",
                    typeof(HttpProtocol), Method, transactException);
                return new KeyValuePair<DateTime, DetailedData<byte[]>?>(requestUtcTime, null);
            }
            catch (HttpRequestException transactException)
            {
                _logger.LogWarning(
                    "Received exception when performing an {TransactionType} - {HttpMethod} call - {Exception}",
                    typeof(HttpProtocol), Method, transactException);
                return new KeyValuePair<DateTime, DetailedData<byte[]>?>(requestUtcTime, null);
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(_transactorConfiguration.MessageSendRetriesIntervalMs));
        }

        _logger.LogDebug("Retries exceeded when performing an http request, no response saved");
        return new KeyValuePair<DateTime, DetailedData<byte[]>?>(requestUtcTime, null);
    }

    private HttpRequestMessage CreateRequest(Data<byte[]> data, string requestUri)
    {
        var requestData = new HttpRequestMessage(GetHttpMethod(), data.MetaData?.Http?.Uri?.AbsoluteUri ?? requestUri)
        {
            Content = new ByteArrayContent(data.Body ?? [])
        };

        return AddHeadersToRequest(requestData,
            data.MetaData?.Http?.Headers ?? _transactorConfiguration.Headers,
            data.MetaData?.Http?.RequestHeaders ?? _transactorConfiguration.RequestHeaders);
    }

    /// <summary>
    /// Adds headers to the given content.
    /// </summary>
    private HttpRequestMessage AddHeadersToRequest(HttpRequestMessage message,
        IDictionary<string, string>? contentHeaders, IDictionary<string, string>? requestHeaders)
    {
        if (contentHeaders == null && requestHeaders == null) return message;
        // Add Content Headers
        foreach (var key in contentHeaders?.Keys ?? Enumerable.Empty<string>())
            message.Content?.Headers.Add(key, contentHeaders?[key]);
        // Add Request Headers
        foreach (var key in requestHeaders?.Keys ?? Enumerable.Empty<string>())
            message.Headers.Add(key, requestHeaders?[key]);
        return message;
    }

    private HttpMethod GetHttpMethod()
    {
        return Method switch
        {
            HttpMethods.Post => HttpMethod.Post,
            HttpMethods.Put => HttpMethod.Put,
            HttpMethods.Get => HttpMethod.Get,
            HttpMethods.Delete => HttpMethod.Delete,
            _ => throw new ArgumentOutOfRangeException(
                $"Invalid Http Method - {Method}, selected while retrieving Method from configuration")
        };
    }
}
