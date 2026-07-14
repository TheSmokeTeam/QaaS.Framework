using Amazon.S3;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Protocols.Extentions;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class S3RetryLoggingTests
{
    private sealed class ExceptionCapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Entries.Add((logLevel, formatter(state, exception), exception));
    }

    [Test]
    public void TerminalFailure_LogsConciseContextWithoutDuplicateExceptionBlock()
    {
        var logger = new ExceptionCapturingLogger();
        var terminalFailure = new AmazonS3Exception("wire failed")
        {
            ErrorCode = "ServiceUnavailable",
        };

        var thrown = Assert.Throws<AmazonS3Exception>(() =>
            S3Extentions.RunS3OperationWithRetryMechanism<int>(
                () => throw terminalFailure,
                "reading object events/current.json",
                logger: logger
            )
        );

        var error = logger.Entries.Single();
        Assert.Multiple(() =>
        {
            Assert.That(thrown, Is.SameAs(terminalFailure));
            Assert.That(error.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(error.Exception, Is.Null);
            Assert.That(error.Message, Does.Contain("reading object events/current.json"));
            Assert.That(error.Message, Does.Contain(nameof(AmazonS3Exception)));
            Assert.That(error.Message, Does.Contain("wire failed"));
        });
    }

    [Test]
    public void ThrottleRetry_LogsConciseContextAndStillRetries()
    {
        var logger = new ExceptionCapturingLogger();
        var attempts = 0;

        var result = S3Extentions.RunS3OperationWithRetryMechanism(
            () =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new AmazonS3Exception("slow down") { ErrorCode = "TooManyRequests" };
                }

                return 7;
            },
            "reading object events/current.json",
            maxRetryCount: 2,
            logger: logger
        );

        var warning = logger.Entries.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(7));
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(warning.Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(warning.Exception, Is.Null);
            Assert.That(warning.Message, Does.Contain("Retry 1/2"));
            Assert.That(warning.Message, Does.Contain("slow down"));
        });
    }
}
