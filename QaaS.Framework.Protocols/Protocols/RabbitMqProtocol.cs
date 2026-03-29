using Microsoft.Extensions.Logging;
using QaaS.Framework.Protocols.ConfigurationObjects.RabbitMq;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Framework.Serialization;
using RabbitMQ.Client;

namespace QaaS.Framework.Protocols.Protocols;

public class RabbitMqProtocol : IReader, ISender, IDisposable
{
    private const string DefaultName = "QaaS";
    private readonly ILogger _logger;
    private IConnection _connection = null!;
    private IChannel _channel = null!;

    private readonly string? _queueName;
    private readonly RabbitMq? _defaultMetaData;
    private string ExchangeName { get; set; } = string.Empty;
    private string RoutingKey { get; set; } = string.Empty;

    private readonly RabbitMqReaderConfig? _rabbitMqReaderConfig;
    private readonly string _defaultQueueName = $"{DefaultName}_{Guid.NewGuid()}";

    private ConnectionFactory ConnectionFactory { get; set; }

    public RabbitMqProtocol(RabbitMqReaderConfig configurations, ILogger logger) : this(
        (BaseRabbitMqConfig)configurations, logger)
    {
        RoutingKey = configurations.RoutingKey;
        ExchangeName = configurations.ExchangeName ?? string.Empty;
        _queueName = configurations.QueueName;
        _rabbitMqReaderConfig = configurations;
    }

    public RabbitMqProtocol(RabbitMqSenderConfig configurations, ILogger logger) : this(
        (BaseRabbitMqConfig)configurations, logger)
    {
        // When sending directly to a queue the exchange value is an empty string (rabbitmq's default exchange which is
        // implicitly connected to every queue), and the routing key represents the queue's name.
        RoutingKey = configurations.QueueName ?? configurations.RoutingKey;
        ExchangeName = configurations.QueueName != null ? string.Empty : configurations.ExchangeName!;

        _defaultMetaData = new RabbitMq
        {
            RoutingKey = RoutingKey,
            Expiration = configurations.Expiration,
            ContentType = configurations.ContentType,
            Type = configurations.Type,
            Headers = configurations.Headers,
        };
    }

    public RabbitMqProtocol(BaseRabbitMqConfig configurations, ILogger logger)
    {
        _logger = logger;
        ConnectionFactory = new ConnectionFactory
        {
            HostName = configurations.Host!, Port = configurations.Port,
            UserName = configurations.Username, Password = configurations.Password,
            VirtualHost = configurations.VirtualHost,
            ContinuationTimeout = TimeSpan.FromSeconds(configurations.ContinuationTimeoutSeconds),
            RequestedConnectionTimeout = TimeSpan.FromSeconds(configurations.RequestedConnectionTimeoutSeconds),
            HandshakeContinuationTimeout = TimeSpan.FromSeconds(configurations.HandshakeContinuationTimeoutSeconds),
        };
    }

    public SerializationType? GetSerializationType() => null;

    public DetailedData<object>? Read(TimeSpan timeout)
    {
        _channel.QueueDeclarePassiveAsync(_queueName ?? _defaultQueueName).GetAwaiter()
            .GetResult(); // Before reading check if queue exists

        var timoutToken = new CancellationTokenSource(timeout).Token;
        while (!timoutToken.IsCancellationRequested)
        {
            var message = _channel.BasicGetAsync(_queueName ?? _defaultQueueName, true).GetAwaiter().GetResult();
            if (message == null) continue;
            _logger.LogDebug("Read message in bytes from Queue {QueueName}", _queueName);
            return new DetailedData<object>
            {
                Body = message.Body.ToArray(),
                MetaData = new MetaData
                {
                    RabbitMq = new RabbitMq
                    {
                        RoutingKey = message.RoutingKey,
                        Headers = message.BasicProperties.Headers,
                        Expiration = message.BasicProperties.Expiration,
                        ContentType = message.BasicProperties.ContentType,
                        Type = message.BasicProperties.Type
                    }
                },
                Timestamp = DateTime.UtcNow
            };
        }

        return null;
    }

    public DetailedData<object> Send(Data<object> dataToSend)
    {
        var headers = NormalizeHeaders(dataToSend.MetaData?.RabbitMq?.Headers ?? _defaultMetaData!.Headers);
        var expiration = NormalizeOptionalString(dataToSend.MetaData?.RabbitMq?.Expiration ?? _defaultMetaData!.Expiration);
        var contentType = NormalizeOptionalString(dataToSend.MetaData?.RabbitMq?.ContentType ?? _defaultMetaData!.ContentType);
        var type = NormalizeOptionalString(dataToSend.MetaData?.RabbitMq?.Type ?? _defaultMetaData!.Type);

        BasicProperties? basicProperties = null;
        if (headers != null || expiration != null || contentType != null || type != null)
        {
            var configuredProperties = new BasicProperties();
            if (headers != null)
                configuredProperties.Headers = headers;
            if (expiration != null)
                configuredProperties.Expiration = expiration;
            if (contentType != null)
                configuredProperties.ContentType = contentType;
            if (type != null)
                configuredProperties.Type = type;

            basicProperties = configuredProperties;
        }

        _channel.ExchangeDeclarePassiveAsync(ExchangeName).GetAwaiter()
            .GetResult(); // Before sending check if exchange exists
        _channel.BasicPublishAsync<BasicProperties>(ExchangeName,
                dataToSend.MetaData?.RabbitMq?.RoutingKey ?? RoutingKey, true,
                basicProperties!, dataToSend.CastObjectData<byte[]>().Body).GetAwaiter()
            .GetResult(); // Assumes data is byte[]
        _logger.LogDebug("Sent message in bytes to Exchange {ExchangeName}, Queue {QueueName}", ExchangeName,
            _queueName);
        return dataToSend.CloneDetailed();
    }

    private static IDictionary<string, object?>? NormalizeHeaders(IDictionary<string, object?>? headers) =>
        headers is { Count: > 0 } ? headers : null;

    private static string? NormalizeOptionalString(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }

    public void Connect()
    {
        _connection = ConnectionFactory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
        if (_rabbitMqReaderConfig == null) return;
        if (_queueName == null)
            _channel.QueueDeclareAsync(_defaultQueueName, arguments: new Dictionary<string, object?>
            {
                {
                    "x-expires",
                    (int)TimeSpan.FromMilliseconds(_rabbitMqReaderConfig.CreatedQueueTimeToExpireMs).TotalMilliseconds
                }
            }).GetAwaiter().GetResult();

        _channel.QueueBindAsync(_queueName ?? _defaultQueueName, ExchangeName, RoutingKey).GetAwaiter().GetResult();

    }

    public void Disconnect()
    {
        _channel.QueueDeleteAsync(_defaultQueueName).GetAwaiter().GetResult();
        _channel.CloseAsync().GetAwaiter().GetResult();
        _connection.CloseAsync().GetAwaiter().GetResult();
    }
}
