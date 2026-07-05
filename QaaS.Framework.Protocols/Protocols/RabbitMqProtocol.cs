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

    public RabbitMqProtocol(RabbitMqReaderConfig configurations, ILogger logger)
        : this((BaseRabbitMqConfig)configurations, logger)
    {
        RoutingKey = configurations.RoutingKey;
        ExchangeName = configurations.ExchangeName ?? string.Empty;
        _queueName = configurations.QueueName;
        _rabbitMqReaderConfig = configurations;
    }

    public RabbitMqProtocol(RabbitMqSenderConfig configurations, ILogger logger)
        : this((BaseRabbitMqConfig)configurations, logger)
    {
        // When sending directly to a queue the exchange value is an empty string (rabbitmq's default exchange which is
        // implicitly connected to every queue), and the routing key represents the queue's name.
        RoutingKey = configurations.QueueName ?? configurations.RoutingKey;
        ExchangeName =
            configurations.QueueName != null ? string.Empty : configurations.ExchangeName!;

        _defaultMetaData = new RabbitMq
        {
            RoutingKey = RoutingKey,
            AppId = configurations.AppId,
            ClusterId = configurations.ClusterId,
            ContentEncoding = configurations.ContentEncoding,
            ContentType = configurations.ContentType,
            CorrelationId = configurations.CorrelationId,
            DeliveryMode = configurations.DeliveryMode,
            Expiration = configurations.Expiration,
            Headers = configurations.Headers,
            MessageId = configurations.MessageId,
            Persistent = configurations.Persistent,
            Priority = configurations.Priority,
            ReplyTo = configurations.ReplyTo,
            TimestampUnixTime = configurations.TimestampUnixTime,
            Type = configurations.Type,
            UserId = configurations.UserId,
        };
    }

    public RabbitMqProtocol(BaseRabbitMqConfig configurations, ILogger logger)
    {
        _logger = logger;
        ConnectionFactory = new ConnectionFactory
        {
            HostName = configurations.Host!,
            Port = configurations.Port,
            UserName = configurations.Username,
            Password = configurations.Password,
            VirtualHost = configurations.VirtualHost,
            ContinuationTimeout = TimeSpan.FromSeconds(configurations.ContinuationTimeoutSeconds),
            RequestedConnectionTimeout = TimeSpan.FromSeconds(
                configurations.RequestedConnectionTimeoutSeconds
            ),
            HandshakeContinuationTimeout = TimeSpan.FromSeconds(
                configurations.HandshakeContinuationTimeoutSeconds
            ),
        };
    }

    public SerializationType? GetSerializationType() => null;

    public DetailedData<object>? Read(TimeSpan timeout)
    {
        _channel.QueueDeclarePassiveAsync(_queueName ?? _defaultQueueName).GetAwaiter().GetResult(); // Before reading check if queue exists

        var timoutToken = new CancellationTokenSource(timeout).Token;
        while (!timoutToken.IsCancellationRequested)
        {
            var message = _channel
                .BasicGetAsync(_queueName ?? _defaultQueueName, true)
                .GetAwaiter()
                .GetResult();
            if (message == null)
                continue;
            _logger.LogDebug("Read message in bytes from Queue {QueueName}", _queueName);
            return new DetailedData<object>
            {
                Body = message.Body.ToArray(),
                MetaData = new MetaData
                {
                    RabbitMq = new RabbitMq
                    {
                        RoutingKey = message.RoutingKey,
                        AppId = message.BasicProperties.IsAppIdPresent()
                            ? message.BasicProperties.AppId
                            : null,
                        ClusterId = message.BasicProperties.IsClusterIdPresent()
                            ? message.BasicProperties.ClusterId
                            : null,
                        ContentEncoding = message.BasicProperties.IsContentEncodingPresent()
                            ? message.BasicProperties.ContentEncoding
                            : null,
                        ContentType = message.BasicProperties.IsContentTypePresent()
                            ? message.BasicProperties.ContentType
                            : null,
                        CorrelationId = message.BasicProperties.IsCorrelationIdPresent()
                            ? message.BasicProperties.CorrelationId
                            : null,
                        DeliveryMode = message.BasicProperties.IsDeliveryModePresent()
                            ? (int)message.BasicProperties.DeliveryMode
                            : null,
                        Expiration = message.BasicProperties.IsExpirationPresent()
                            ? message.BasicProperties.Expiration
                            : null,
                        Headers = message.BasicProperties.IsHeadersPresent()
                            ? message.BasicProperties.Headers
                            : null,
                        MessageId = message.BasicProperties.IsMessageIdPresent()
                            ? message.BasicProperties.MessageId
                            : null,
                        Persistent = message.BasicProperties.IsDeliveryModePresent()
                            ? message.BasicProperties.Persistent
                            : null,
                        Priority = message.BasicProperties.IsPriorityPresent()
                            ? message.BasicProperties.Priority
                            : null,
                        ReplyTo = message.BasicProperties.IsReplyToPresent()
                            ? message.BasicProperties.ReplyTo
                            : null,
                        TimestampUnixTime = message.BasicProperties.IsTimestampPresent()
                            ? message.BasicProperties.Timestamp.UnixTime
                            : null,
                        Type = message.BasicProperties.IsTypePresent()
                            ? message.BasicProperties.Type
                            : null,
                        UserId = message.BasicProperties.IsUserIdPresent()
                            ? message.BasicProperties.UserId
                            : null,
                    },
                },
                Timestamp = DateTime.UtcNow,
            };
        }

        return null;
    }

    public DetailedData<object> Send(Data<object> dataToSend)
    {
        var metadata = NormalizeRabbitMqMetadata(dataToSend.MetaData?.RabbitMq);
        var routingKey = metadata.RoutingKey ?? RoutingKey;
        var body = dataToSend.CastObjectData<byte[]>().Body;

        _channel.ExchangeDeclarePassiveAsync(ExchangeName).GetAwaiter().GetResult(); // Before sending check if exchange exists

        var basicProperties = CreateBasicProperties(metadata);
        if (basicProperties == null)
        {
            _channel
                .BasicPublishAsync(ExchangeName, routingKey, true, body)
                .GetAwaiter()
                .GetResult();
        }
        else
        {
            _channel
                .BasicPublishAsync(ExchangeName, routingKey, true, basicProperties, body)
                .GetAwaiter()
                .GetResult(); // Assumes data is byte[]
        }

        _logger.LogDebug(
            "Sent message in bytes to Exchange {ExchangeName}, Queue {QueueName}",
            ExchangeName,
            _queueName
        );
        return dataToSend.CloneDetailed();
    }

    private static IDictionary<string, object?>? NormalizeHeaders(
        IDictionary<string, object?>? headers
    ) => headers is { Count: > 0 } ? headers : null;

    private static string? NormalizeOptionalString(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private RabbitMq NormalizeRabbitMqMetadata(RabbitMq? metadata)
    {
        var defaultMetadata =
            _defaultMetaData
            ?? throw new InvalidOperationException(
                "RabbitMQ sender metadata defaults were not initialized."
            );
        var resolvedDeliveryMode = metadata?.DeliveryMode ?? defaultMetadata.DeliveryMode;
        var resolvedPriority = metadata?.Priority ?? defaultMetadata.Priority;
        var resolvedTimestamp = metadata?.TimestampUnixTime ?? defaultMetadata.TimestampUnixTime;

        return new RabbitMq
        {
            RoutingKey = NormalizeOptionalString(metadata?.RoutingKey) ?? RoutingKey,
            AppId = NormalizeOptionalString(metadata?.AppId ?? defaultMetadata.AppId),
            ClusterId = NormalizeOptionalString(metadata?.ClusterId ?? defaultMetadata.ClusterId),
            ContentEncoding = NormalizeOptionalString(
                metadata?.ContentEncoding ?? defaultMetadata.ContentEncoding
            ),
            ContentType = NormalizeOptionalString(
                metadata?.ContentType ?? defaultMetadata.ContentType
            ),
            CorrelationId = NormalizeOptionalString(
                metadata?.CorrelationId ?? defaultMetadata.CorrelationId
            ),
            DeliveryMode = NormalizeDeliveryMode(resolvedDeliveryMode),
            Expiration = NormalizeOptionalString(
                metadata?.Expiration ?? defaultMetadata.Expiration
            ),
            Headers = NormalizeHeaders(metadata?.Headers ?? defaultMetadata.Headers),
            MessageId = NormalizeOptionalString(metadata?.MessageId ?? defaultMetadata.MessageId),
            Persistent = metadata?.Persistent ?? defaultMetadata.Persistent,
            Priority = NormalizePriority(resolvedPriority),
            ReplyTo = NormalizeOptionalString(metadata?.ReplyTo ?? defaultMetadata.ReplyTo),
            TimestampUnixTime = NormalizeTimestamp(resolvedTimestamp),
            Type = NormalizeOptionalString(metadata?.Type ?? defaultMetadata.Type),
            UserId = NormalizeOptionalString(metadata?.UserId ?? defaultMetadata.UserId),
        };
    }

    private static BasicProperties? CreateBasicProperties(RabbitMq metadata)
    {
        var basicProperties = new BasicProperties();
        var hasProperties = false;

        SetOptionalStringProperty(
            metadata.AppId,
            value => basicProperties.AppId = value,
            ref hasProperties
        );
        SetOptionalStringProperty(
            metadata.ClusterId,
            value => basicProperties.ClusterId = value,
            ref hasProperties
        );
        SetOptionalStringProperty(
            metadata.ContentEncoding,
            value => basicProperties.ContentEncoding = value,
            ref hasProperties
        );
        SetOptionalStringProperty(
            metadata.ContentType,
            value => basicProperties.ContentType = value,
            ref hasProperties
        );
        SetOptionalStringProperty(
            metadata.CorrelationId,
            value => basicProperties.CorrelationId = value,
            ref hasProperties
        );
        SetOptionalStringProperty(
            metadata.Expiration,
            value => basicProperties.Expiration = value,
            ref hasProperties
        );
        SetOptionalStringProperty(
            metadata.MessageId,
            value => basicProperties.MessageId = value,
            ref hasProperties
        );
        SetOptionalStringProperty(
            metadata.ReplyTo,
            value => basicProperties.ReplyTo = value,
            ref hasProperties
        );
        SetOptionalStringProperty(
            metadata.Type,
            value => basicProperties.Type = value,
            ref hasProperties
        );
        SetOptionalStringProperty(
            metadata.UserId,
            value => basicProperties.UserId = value,
            ref hasProperties
        );

        if (metadata.Headers != null)
        {
            basicProperties.Headers = metadata.Headers;
            hasProperties = true;
        }

        if (metadata.DeliveryMode.HasValue)
        {
            basicProperties.DeliveryMode = (DeliveryModes)metadata.DeliveryMode.Value;
            hasProperties = true;
        }
        else if (metadata.Persistent.HasValue)
        {
            basicProperties.Persistent = metadata.Persistent.Value;
            hasProperties = true;
        }

        if (metadata.Priority.HasValue)
        {
            basicProperties.Priority = (byte)metadata.Priority.Value;
            hasProperties = true;
        }

        if (metadata.TimestampUnixTime.HasValue)
        {
            basicProperties.Timestamp = new AmqpTimestamp(metadata.TimestampUnixTime.Value);
            hasProperties = true;
        }

        return hasProperties ? basicProperties : null;
    }

    private static void SetOptionalStringProperty(
        string? value,
        Action<string> setProperty,
        ref bool hasProperties
    )
    {
        if (value == null)
            return;

        setProperty(value);
        hasProperties = true;
    }

    private static int? NormalizeDeliveryMode(int? value)
    {
        if (value is null)
            return null;
        if (value is < 1 or > 2)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "RabbitMQ delivery mode must be 1 (transient) or 2 (persistent)."
            );

        return value;
    }

    private static int? NormalizePriority(int? value)
    {
        if (value is null)
            return null;
        if (value is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "RabbitMQ priority must be between 0 and 255."
            );

        return value;
    }

    private static long? NormalizeTimestamp(long? value)
    {
        if (value is null)
            return null;
        if (value < 0)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "RabbitMQ timestamp must be a non-negative Unix time value."
            );

        return value;
    }

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }

    public void Connect()
    {
        _connection = ConnectionFactory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
        if (_rabbitMqReaderConfig == null)
            return;
        if (_queueName == null)
            _channel
                .QueueDeclareAsync(
                    _defaultQueueName,
                    arguments: new Dictionary<string, object?>
                    {
                        {
                            "x-expires",
                            (int)
                                TimeSpan
                                    .FromMilliseconds(
                                        _rabbitMqReaderConfig.CreatedQueueTimeToExpireMs
                                    )
                                    .TotalMilliseconds
                        },
                    }
                )
                .GetAwaiter()
                .GetResult();

        _channel
            .QueueBindAsync(_queueName ?? _defaultQueueName, ExchangeName, RoutingKey)
            .GetAwaiter()
            .GetResult();
    }

    public void Disconnect()
    {
        _channel.QueueDeleteAsync(_defaultQueueName).GetAwaiter().GetResult();
        _channel.CloseAsync().GetAwaiter().GetResult();
        _connection.CloseAsync().GetAwaiter().GetResult();
    }
}
