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
    private readonly object _sendLock = new();

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
        _socketHost = configuration.Host;
        _socketPort = configuration.Port;
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
        using var cancellation = new CancellationTokenSource(timeout);
        var timeoutToken = cancellation.Token;
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        // Initializing a cancellation token.
        // While the cancellation token is running, continue to read.
        while (!timeoutToken.IsCancellationRequested)
        {
            // Getting the message buffer from Socket communication.
            if (_socket is { Available: 0, Connected: true })
            {
                var remaining = timeout - elapsed.Elapsed;
                if (remaining <= TimeSpan.Zero)
                    return null;
                if (
                    !_socket.Poll(
                        (int)Math.Min(int.MaxValue, Math.Ceiling(remaining.TotalMicroseconds)),
                        SelectMode.SelectRead
                    )
                )
                    continue;
            }
            var message = GetMessage();
            if (message.Length <= 0)
                return null; // TCP EOF: polling again would spin on a permanently readable socket.
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
    /// <returns>Buffer read from Socket connection, truncated to the number of bytes actually received.</returns>
    protected virtual Span<byte> GetMessage()
    {
        var message = new Span<byte>(new byte[_bufferSize!.Value]);
        // Socket.Receive returns the number of bytes actually read; the remainder of the buffer is
        // uninitialized zero padding and must not be treated as payload, otherwise messages are corrupted
        // with trailing zeros and an empty (graceful-close) read is never detected.
        var bytesReceived = _socket!.Receive(message);
        return message[..bytesReceived];
    }

    public DetailedData<object> Send(Data<object> dataToSend)
    {
        var body = dataToSend.CastObjectData<byte[]>().Body ?? [];
        lock (_sendLock)
            SendAll(
                body.Length,
                _socket!.SocketType == SocketType.Stream,
                (offset, count) => _socket.Send(body, offset, count, SocketFlags.None)
            );
        return dataToSend.CloneDetailed();
    }

    internal static void SendAll(int length, bool stream, Func<int, int, int> send)
    {
        var offset = 0;
        do
        {
            var written = send(offset, length - offset);
            if (written < 0 || written > length - offset || (written == 0 && offset < length))
                throw new IOException("Socket closed before the entire payload was sent.");
            offset += written;
            if (!stream && offset != length)
                throw new IOException("Socket did not send the entire datagram.");
        } while (offset < length);
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
