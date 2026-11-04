using QaaS.Framework.Protocols.Utils;
using Trino.Data.ADO.Server;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class TrinoTimeoutTests
{
    [TestCase(10000000L)]
    [TestCase(1000000000L)]
    public void ProviderThreshold_RepresentsTheSameSixtySecondsOnWindowsAndLinux(long frequency)
    {
        var threshold = TrinoTimeoutCommand.EncodeDeadline(60, frequency);
        Assert.That(threshold.Ticks / (double)frequency, Is.EqualTo(60));
        Assert.That(59L * frequency, Is.LessThan(threshold.Ticks));
        Assert.That(61L * frequency, Is.GreaterThan(threshold.Ticks));
    }

    [Test]
    public void Dispose_CancelsOutstandingWork_AndCanBeRepeated()
    {
        using var connection = new TrinoConnection();
        var command = new TrinoTimeoutCommand(connection);
        var token = command.CancellationToken.Token;
        command.Dispose();
        Assert.That(token.IsCancellationRequested, Is.True);
        Assert.DoesNotThrow(() => command.Dispose());
    }

    [Test]
    public void PublicCommandTimeout_RemainsSeconds_AndCancellationRemainsAvailable()
    {
        using var connection = new TrinoConnection();
        using var command = new TrinoTimeoutCommand(connection) { CommandTimeout = 60 };
        Assert.That(command.CommandTimeout, Is.EqualTo(60));
        Assert.That(
            connection.ConnectionSession.Properties.Timeout!.Value.Ticks,
            Is.EqualTo(60L * System.Diagnostics.Stopwatch.Frequency)
        );
        command.Cancel();
        Assert.That(command.CancellationToken.IsCancellationRequested, Is.True);
    }
}
