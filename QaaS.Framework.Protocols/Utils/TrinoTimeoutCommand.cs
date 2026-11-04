using System.Diagnostics;
using Trino.Data.ADO.Server;

namespace QaaS.Framework.Protocols.Utils;

/// <summary>
/// Adapter for the pinned NH.Trino.Client 2025.11.15.6, whose IsTimeout compares
/// Stopwatch.ElapsedTicks with TimeSpan.Ticks. Keep QaaS timeout units in seconds;
/// encode the provider's threshold in the units it actually compares.
/// Revisit this adapter when upgrading that dependency.
/// </summary>
internal sealed class TrinoTimeoutCommand : TrinoCommand
{
    private readonly TrinoConnection _connection;
    private int _timeoutSeconds;
    private bool _disposed;

    internal TrinoTimeoutCommand(TrinoConnection connection)
        : base(connection)
    {
        _connection = connection;
        CommandTimeout = 30;
    }

    public override int CommandTimeout
    {
        get => _timeoutSeconds;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _connection.ConnectionSession.Properties.Timeout = EncodeDeadline(
                value,
                Stopwatch.Frequency
            );
            _timeoutSeconds = value;
        }
    }

    internal static TimeSpan EncodeDeadline(int seconds, long stopwatchFrequency) =>
        TimeSpan.FromTicks(checked((long)seconds * stopwatchFrequency));

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            // Also cancel outstanding page work after an early streaming-enumerator disposal.
            CancellationToken.Cancel();
            CancellationToken.Dispose();
        }
        base.Dispose(disposing);
    }
}
