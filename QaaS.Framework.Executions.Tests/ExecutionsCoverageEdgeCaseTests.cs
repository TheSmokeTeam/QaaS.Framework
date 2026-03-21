using System.Reflection;
using QaaS.Framework.Executions.Loaders;
using QaaS.Framework.Executions.Options;

namespace QaaS.Framework.Executions.Tests;

[TestFixture]
public class ExecutionsCoverageEdgeCaseTests
{
    private sealed record FileLoggerOptions : LoggerOptions;

    private sealed class FileConfiguredRunner : IRunner
    {
        public void Run()
        {
        }
    }

    private sealed class FileConfiguredLoader(FileLoggerOptions options)
        : BaseLoader<FileLoggerOptions, FileConfiguredRunner>(options)
    {
        public override FileConfiguredRunner GetLoadedRunner() => new();
    }

    [Test]
    public void BaseLoader_UsesLoggerConfigurationFile_WhenProvided()
    {
        var loggerConfigFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.yaml");
        File.WriteAllText(loggerConfigFile,
            """
            Serilog:
              MinimumLevel:
                Default: Debug
            """);

        try
        {
            var loader = new FileConfiguredLoader(new FileLoggerOptions
            {
                LoggerConfigurationFilePath = loggerConfigFile,
                LoggerLevel = Serilog.Events.LogEventLevel.Error
            });

            Assert.That(loader.GetLoadedRunner(), Is.TypeOf<FileConfiguredRunner>());
        }
        finally
        {
            File.Delete(loggerConfigFile);
        }
    }

    [Test]
    public void ExecutionLogging_DefaultLoggers_AreInitialized()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ExecutionLogging.DefaultSerilogLogger, Is.Not.Null);
            Assert.That(ExecutionLogging.DefaultLogger, Is.Not.Null);
        });
    }

    [Test]
    public void BuildDefaultSerilogLogger_DoesNotEmitElasticWarnings_WhenSendLogsWasNotRequested()
    {
        var buildMethod = typeof(ExecutionLogging).GetMethod("BuildDefaultSerilogLogger",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var logger = (Serilog.ILogger)buildMethod.Invoke(null, null)!;
            logger.Information("default logger initialized");
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        var combinedOutput = stdout + Environment.NewLine + stderr;
        Assert.That(combinedOutput, Does.Not.Contain("Elasticsearch logging is enabled"));
    }
}
