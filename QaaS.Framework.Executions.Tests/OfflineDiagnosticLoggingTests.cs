using System.Text.Json;
using CommandLine;
using QaaS.Framework.Executions.CommandLineBuilders;
using QaaS.Framework.Executions.Loaders;
using QaaS.Framework.Executions.Options;
using Serilog;
using Serilog.Events;

namespace QaaS.Framework.Executions.Tests;

[TestFixture]
[NonParallelizable]
public class OfflineDiagnosticLoggingTests
{
    private sealed record TestOptions : LoggerOptions;

    [Verb("run")]
    private sealed record RunOptions : LoggerOptions;

    private sealed class TestRunner : IRunner
    {
        public void Run() { }
    }

    private sealed class TestLoader(TestOptions options)
        : BaseLoader<TestOptions, TestRunner>(options)
    {
        public Serilog.ILogger DiagnosticLogger => SerilogLogger;

        public override TestRunner GetLoadedRunner() => new();
    }

    [Test]
    public void Parser_AcceptsJsonLogsWithoutEnablingNetworkLogging()
    {
        using var parser = ParserBuilder.BuildParser();
        var parsed = parser.ParseArguments<RunOptions>(["--json-logs"]);
        Assert.That(parsed.Tag, Is.EqualTo(ParserResultType.Parsed));
        var options = ((Parsed<RunOptions>)parsed).Value;
        Assert.Multiple(() =>
        {
            Assert.That(options.JsonLogs, Is.True);
            Assert.That(options.SendLogs, Is.False);
        });
    }

    [Test]
    public void JsonLogs_PreservePropertiesExceptionsAndConfiguredLevel()
    {
        var output = Capture(
            new TestOptions
            {
                JsonLogs = true,
                LoggerLevel = LogEventLevel.Warning,
                DisableElasticDefaults = true,
            },
            logger =>
            {
                logger.Information("Filtered event");
                logger.Error(
                    new InvalidOperationException("outer", new ArgumentException("inner")),
                    "Failure in {Phase}",
                    "BuildExecutions"
                );
            }
        );
        using var document = JsonDocument.Parse(output);
        Assert.Multiple(() =>
        {
            Assert.That(
                document.RootElement.GetProperty("Properties").GetProperty("Phase").GetString(),
                Is.EqualTo("BuildExecutions")
            );
            Assert.That(
                document.RootElement.GetProperty("Exception").GetString(),
                Does.Contain("inner")
            );
            Assert.That(output, Does.Not.Contain("Filtered event"));
        });
    }

    [Test]
    public void DefaultConsole_RemainsText()
    {
        var output = Capture(
            new TestOptions { DisableElasticDefaults = true },
            logger => logger.Information("Text remains available")
        );
        Assert.That(output, Does.Contain("Text remains available"));
        Assert.That(output.TrimStart(), Does.Not.StartWith("{"));
    }

    [Test]
    public void ExplicitConfiguration_TakesPrecedenceOverJsonFlag()
    {
        var file = Path.Combine(Path.GetTempPath(), $"qaas-logger-{Guid.NewGuid():N}.yaml");
        try
        {
            File.WriteAllText(
                file,
                "Serilog:\n  MinimumLevel: Information\n  WriteTo:\n    - Name: Console\n      Args:\n        outputTemplate: 'CUSTOM {Message:lj}{NewLine}'\n"
            );
            var output = Capture(
                new TestOptions { JsonLogs = true, LoggerConfigurationFilePath = file },
                logger => logger.Information("Explicit configuration")
            );
            Assert.That(output.Trim(), Is.EqualTo("CUSTOM Explicit configuration"));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static string Capture(TestOptions options, Action<Serilog.ILogger> write)
    {
        var original = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            var loader = new TestLoader(options);
            using var logger = (IDisposable)loader.DiagnosticLogger;
            write(loader.DiagnosticLogger);
        }
        finally
        {
            Console.SetOut(original);
        }
        return output.ToString();
    }
}
