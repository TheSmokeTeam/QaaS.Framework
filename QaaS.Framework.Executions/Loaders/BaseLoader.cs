using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations;
using QaaS.Framework.Configurations.ConfigurationBuilderExtensions;
using QaaS.Framework.Configurations.CustomExceptions;
using QaaS.Framework.Executions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace QaaS.Framework.Executions.Loaders;

/// <summary>
/// Responsible for loading <see cref="IRunner"/> objects based on the provided options
/// </summary>
public abstract class BaseLoader<TOptions, TRunner> where TOptions : LoggerOptions where TRunner : IRunner
{
    protected readonly TOptions Options;
    protected ILogger Logger;
    protected readonly Serilog.ILogger SerilogLogger;
    protected readonly string? ExecutionId;

    protected BaseLoader(TOptions options, string? executionId = null)
    {
        ValidateOptions(options);
        Options = options;
        SerilogLogger = BuildSerilogLogger(options);
        Logger = BuildLogger(SerilogLogger);
        ExecutionId = executionId;
    }

    private Serilog.ILogger BuildSerilogLogger(TOptions options)
    {
        var configuredLogLevel =  options.LoggerLevel ?? LogEventLevel.Information;
        var loggerConfiguration = options.LoggerConfigurationFilePath != null
            ? CreateLoggerConfigurationFromLoggerConfigurationFile(options.LoggerConfigurationFilePath, options.LoggerLevel)
            : new LoggerConfiguration()
                .MinimumLevel.Is(options.SendLogs ? LogEventLevel.Verbose : configuredLogLevel)
                .WriteTo.Console(configuredLogLevel);

        var warnings = new List<string>();
        var serilogLogger = AddElasticSinkIfEnabled(loggerConfiguration, options, warnings).CreateLogger();
        foreach (var warning in warnings)
        {
            serilogLogger.Warning("{WarningMessage}", warning);
        }

        return serilogLogger;
    }

    private LoggerConfiguration AddElasticSinkIfEnabled(
        LoggerConfiguration config,
        TOptions options,
        ICollection<string> warnings)
        => options.SendLogs
            ? config.AddQaaSElasticSink(options.ElasticUri, options.ElasticUsername, options.ElasticPassword, warnings.Add)
            : config;

    private ILogger BuildLogger(Serilog.ILogger serilogLogger) =>
        new SerilogLoggerFactory(serilogLogger).CreateLogger(GetType().Name);

    private static LoggerConfiguration CreateLoggerConfigurationFromLoggerConfigurationFile(
        string loggerConfigurationFilePath,
        LogEventLevel? loggerLevel) =>
        loggerLevel != null
            ? new LoggerConfiguration()
                .ReadFrom.Configuration(new ConfigurationBuilder().AddYaml(loggerConfigurationFilePath).Build())
                .MinimumLevel.Is(loggerLevel.Value)
            : new LoggerConfiguration()
                .ReadFrom.Configuration(new ConfigurationBuilder().AddYaml(loggerConfigurationFilePath).Build());

    private static void ValidateOptions(TOptions options)
    {
        var commandLineValidationResults = new List<ValidationResult>();
        ValidationUtils.TryValidateObjectRecursive(options, commandLineValidationResults);
        if (commandLineValidationResults.Any())
            throw new InvalidConfigurationsException(
                "Given command arguments are not valid. The validation results are: \n- " +
                string.Join("\n- ", commandLineValidationResults.Select(result =>
                    result.ErrorMessage)));
    }


    /// <summary>
    /// Gets the loaded <see cref="IRunner"/> object based on the provided options 
    /// </summary>
    /// <returns></returns>
    public abstract TRunner GetLoadedRunner();
}
