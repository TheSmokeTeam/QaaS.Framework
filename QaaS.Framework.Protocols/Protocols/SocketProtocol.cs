using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Protocols.ConfigurationObjects.Socket;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.Protocols.Protocols;

public class SocketProtocol : IReader, ISender, IDisposable
{
    private readonly ILogger _logger;

    private readonly Socket? _socket;
    private readonly string? _socketHost;
    private readonly int? _socketPort;
    private readonly int? _bufferSize;

    public SocketProtocol(SocketReaderConfig configuration, ILogger logger)
    {
        _logger = logger;
        _socket = new Socket(configuration.AddressFamily,
            configuration.SocketType,
            configuration.ProtocolType!.Value)
        {
            ReceiveBufferSize = configuration.BufferSize,
            ReceiveTimeout = configuration.ReceiveTimeoutMs
        };
        _bufferSize = configuration.BufferSize;
    }

    public SocketProtocol(SocketSenderConfig configuration, ILogger logger)
    {
        _logger = logger;
        _socket = new Socket(configuration.AddressFamily, configuration.SocketType, configuration.ProtocolType!.Value)
        {
            SendBufferSize = configuration.BufferSize,
            NoDelay = !configuration.NagleAlgorithm,
            LingerState = new LingerOption(configuration.LingerTimeSeconds.HasValue,
                configuration.LingerTimeSeconds ?? 0),
            SendTimeout = configuration.SendTimeoutMs
        };
        _socketHost = configuration.Host;
        _socketPort = configuration.Port;
    }
    
    public SerializationType? GetSerializationType() => null;

    public DetailedData<object>? Read(TimeSpan timeout)
    {
        var timeoutToken = new CancellationTokenSource(timeout).Token;
        // Initializing a cancellation token.
        // While the cancellation token is running, continue to read.
        while (!timeoutToken.IsCancellationRequested)
        {
            // Getting the message buffer from Socket communication.
            if (_socket is { Available: 0, Connected: true }) continue;
            var message = GetMessage();
            if (message.Length <= 0) continue;
            _logger.LogDebug("Received {NumberOfReceivedBytes} bytes from socket", message.Length);
            return new DetailedData<object> { Body = message.ToArray(), Timestamp = DateTime.UtcNow };
        }

        return null;
    }

    /// <summary>
    /// Method to receive message from Socket connection, overridable for
    /// mocking and implementing other Socket connection data fetches.
    /// </summary>
    /// <returns>Buffer read from Socket connection</returns>
    protected virtual Span<byte> GetMessage()
    {
        var message = new Span<byte>(new byte[_bufferSize!.Value]);
        _socket!.Receive(message);
        return message;
    }

    public DetailedData<object> Send(Data<object> dataToSend)
    {
        _socket!.Send(dataToSend.CastObjectData<byte[]>().Body ?? [] ); // Assumes data is byte[]
        return dataToSend.CloneDetailed();
    }

    public void Connect()
    {
        _socket?.Connect(_socketHost!, _socketPort!.Value);
    }

    public void Disconnect()
    {
        _socket?.Shutdown(SocketShutdown.Both);
        _socket?.Close();
    }

    public void Dispose()
    {
        _socket?.Dispose();
    }
}