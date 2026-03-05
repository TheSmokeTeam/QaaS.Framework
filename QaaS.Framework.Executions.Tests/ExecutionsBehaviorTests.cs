using CommandLine;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomExceptions;
using QaaS.Framework.Executions.CommandLineBuilders;
using QaaS.Framework.Executions.Loaders;
using QaaS.Framework.Executions.Options;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.ExecutionObjects;
using Serilog;
using Serilog.Events;

namespace QaaS.Framework.Executions.Tests;

[TestFixture]
public class ExecutionsBehaviorTests
{
    private sealed class TestRunner : IRunner
    {
        public bool DidRun { get; private set; }
        public void Run() => DidRun = true;
    }

    private sealed record TestLoaderOptions : LoggerOptions
    {
    }

    private sealed record InvalidLoaderOptions : LoggerOptions
    {
        [Required]
        public string? RequiredField { get; init; }
    }

    private sealed class TestLoader(TestLoaderOptions options) : BaseLoader<TestLoaderOptions, TestRunner>(options)
    {
        public override TestRunner GetLoadedRunner() => new();
    }

    private sealed class InvalidOptionsLoader(InvalidLoaderOptions options)
        : BaseLoader<InvalidLoaderOptions, TestRunner>(options)
    {
        public override TestRunner GetLoadedRunner() => new();
    }

    private sealed class TestExecution : BaseExecution
    {
        public bool Disposed { get; private set; }

        public override int Start() => 123;

        public override void Dispose() => Disposed = true;
    }

    private sealed class TestExecutionData : IExecutionData
    {
        public List<DataSource> DataSources { get; set; } = [];
    }

    private sealed class TestContext : BaseContext<TestExecutionData>
    {
    }

    private sealed class TestExecutionBuilder : BaseExecutionBuilder<TestContext, TestExecutionData>
    {
        public int BuildDataSourcesCalls { get; private set; }

        protected override IEnumerable<DataSource> BuildDataSources()
        {
            BuildDataSourcesCalls++;
            return [];
        }

        public override BaseExecution Build()
        {
            _ = BuildDataSources();
            return new TestExecution();
        }
    }

    private sealed class ParseOptions
    {
        [Option("mode", Required = true)]
        public LogEventLevel Mode { get; set; }
    }

    [Verb("run")]
    private sealed class RunVerb
    {
        [Option("mode", Required = true)]
        public LogEventLevel Mode { get; set; }
    }

    [Verb("stop")]
    private sealed class StopVerb
    {
        [Option("force")]
        public bool Force { get; set; }
    }

    [Test]
    public void LoggerOptions_DefaultValues_AreNullOrExpectedDefaults()
    {
        var options = new TestLoaderOptions();

        Assert.Multiple(() =>
        {
            Assert.That(options.LoggerLevel, Is.Null);
            Assert.That(options.LoggerConfigurationFilePath, Is.Null);
            Assert.That(options.SendLogs, Is.True);
            Assert.That(options.ElasticUri, Is.Null);
            Assert.That(options.ElasticUsername, Is.Null);
            Assert.That(options.ElasticPassword, Is.Null);
        });
    }

    [Test]
    public void BaseLoader_BuildsLogger_WhenSendLogsDisabled()
    {
        Assert.DoesNotThrow(() => _ = new TestLoader(new TestLoaderOptions { SendLogs = false }));
    }

    [Test]
    public void BaseLoader_DoesNotThrow_WhenElasticSinkHasNoUri()
    {
        Assert.DoesNotThrow(() => _ = new TestLoader(new TestLoaderOptions { SendLogs = true }));
    }

    [Test]
    public void BaseLoader_InvalidOptions_ThrowsInvalidConfigurationsException()
    {
        Assert.Throws<InvalidConfigurationsException>(() => _ = new InvalidOptionsLoader(new InvalidLoaderOptions()));
    }

    [Test]
    public void ParserBuilder_ParsesEnumCaseInsensitively()
    {
        var parser = ParserBuilder.BuildParser();

        var result = parser.ParseArguments<ParseOptions>(["--mode", "warning"]);

        Assert.That(result.Tag, Is.EqualTo(ParserResultType.Parsed));
        var value = ((Parsed<ParseOptions>)result).Value;
        Assert.That(value.Mode, Is.EqualTo(LogEventLevel.Warning));
    }

    [Test]
    public void ParserBuilder_UnknownVerb_ReturnsNotParsed()
    {
        var parser = ParserBuilder.BuildParser();

        var result = parser.ParseArguments<RunVerb, StopVerb>(["unknown"]);

        Assert.That(result.Tag, Is.EqualTo(ParserResultType.NotParsed));
    }

    [Test]
    public void HelpTextBuilder_AddsUsageLine()
    {
        var parser = ParserBuilder.BuildParser();
        var parserResult = parser.ParseArguments<RunVerb, StopVerb>(Array.Empty<string>());
        var helpText = HelpTextBuilder.BuildHelpText(parserResult);

        Assert.That(helpText.ToString(), Does.Contain("Usage:"));
    }

    [Test]
    public void AddQaaSElasticSink_WithoutUri_LogsWarningAndKeepsConfiguration()
    {
        var warnings = new List<string>();
        var config = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console();

        var enrichedConfig = config.AddQaaSElasticSink(warningLogger: warnings.Add);

        Assert.That(enrichedConfig, Is.Not.Null);
        Assert.That(warnings, Has.Count.EqualTo(1));
        Assert.That(warnings[0], Does.Contain("no Elasticsearch URI"));
    }

    [Test]
    public void AddQaaSElasticSink_WithInvalidUri_LogsWarningAndKeepsConfiguration()
    {
        var warnings = new List<string>();
        var config = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console();

        var enrichedConfig = config.AddQaaSElasticSink("not-a-uri", warningLogger: warnings.Add);

        Assert.That(enrichedConfig, Is.Not.Null);
        Assert.That(warnings, Has.Count.EqualTo(1));
        Assert.That(warnings[0], Does.Contain("invalid"));
    }

    [Test]
    public void BaseExecutionBuilder_Build_UsesBuildDataSources()
    {
        var builder = new TestExecutionBuilder();

        var execution = builder.Build();

        Assert.That(execution, Is.TypeOf<TestExecution>());
        Assert.That(builder.BuildDataSourcesCalls, Is.EqualTo(1));
    }

    [Test]
    public void BaseExecution_StartAndDispose_WorkAsExpected()
    {
        var execution = new TestExecution();

        var startCode = execution.Start();
        execution.Dispose();

        Assert.That(startCode, Is.EqualTo(123));
        Assert.That(execution.Disposed, Is.True);
    }
}
