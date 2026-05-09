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
        _socket = new Socket(
            configuration.AddressFamily,
            configuration.SocketType,
            configuration.ProtocolType!.Value
        )
        {
            ReceiveBufferSize = configuration.BufferSize,
            ReceiveTimeout = configuration.ReceiveTimeoutMs,
        };
        _bufferSize = configuration.BufferSize;
    }

    public SocketProtocol(SocketSenderConfig configuration, ILogger logger)
    {
        _logger = logger;
        _socket = new Socket(
            configuration.AddressFamily,
            configuration.SocketType,
            configuration.ProtocolType!.Value
        )
        {
            SendBufferSize = configuration.BufferSize,
            NoDelay = !configuration.NagleAlgorithm,
            LingerState = new LingerOption(
                configuration.LingerTimeSeconds.HasValue,
                configuration.LingerTimeSeconds ?? 0
            ),
            SendTimeout = configuration.SendTimeoutMs,
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
            // Avoid busy-spinning when no data is available: yield the thread for 1 ms
            // so the OS can schedule other work while we wait for bytes to arrive.
            if (_socket is { Available: 0, Connected: true })
            {
                Thread.Sleep(1);
                continue;
            }

            var message = GetMessage();
            if (message.Length <= 0)
                continue;
            _logger.LogDebug("Received {NumberOfReceivedBytes} bytes from socket", message.Length);
            return new DetailedData<object>
            {
                Body = message.ToArray(),
                Timestamp = DateTime.UtcNow,
            };
        }

        return null;
    }

    /// <summary>
    /// Method to receive message from Socket connection, overridable for
    /// mocking and implementing other Socket connection data fetches.
    /// </summary>
    /// <returns>Buffer read from Socket connection containing only the received bytes</returns>
    protected virtual Span<byte> GetMessage()
    {
        var buffer = new byte[_bufferSize!.Value];
        var n = _socket!.Receive(buffer);
        // Return only the bytes actually received; do not return trailing zeros from the buffer.
        return buffer.AsSpan(0, n);
    }

    public DetailedData<object> Send(Data<object> dataToSend)
    {
        _socket!.Send(dataToSend.CastObjectData<byte[]>().Body ?? []); // Assumes data is byte[]
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
