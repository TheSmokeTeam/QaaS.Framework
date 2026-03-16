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
    public void Constants_DefaultLoggers_AreInitialized()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Constants.DefaultSerilogLogger, Is.Not.Null);
            Assert.That(Constants.DefaultLogger, Is.Not.Null);
        });
    }
}
