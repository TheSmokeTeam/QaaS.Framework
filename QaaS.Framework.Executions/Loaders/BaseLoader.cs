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
        var resolvedOptions = ExecutionLogging.ResolveElasticLoggingOptions(options);
        var configuredLogLevel = resolvedOptions.LoggerLevel ?? LogEventLevel.Information;
        var loggerConfiguration = resolvedOptions.LoggerConfigurationFilePath != null
            ? CreateLoggerConfigurationFromLoggerConfigurationFile(
                resolvedOptions.LoggerConfigurationFilePath,
                resolvedOptions.LoggerLevel)
            : new LoggerConfiguration()
                .MinimumLevel.Is(resolvedOptions.SendLogs ? LogEventLevel.Verbose : configuredLogLevel)
                .WriteTo.Console(configuredLogLevel);

        var warnings = new List<string>();
        var serilogLogger = AddElasticSinkIfEnabled(loggerConfiguration, resolvedOptions, warnings).CreateLogger();
        foreach (var warning in warnings)
        {
            serilogLogger.Warning("{WarningMessage}", warning);
        }

        return serilogLogger;
    }

    private LoggerConfiguration AddElasticSinkIfEnabled(
        LoggerConfiguration config,
        ResolvedElasticLoggingOptions options,
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
        {
            var message = DiagnosticMessageFormatter.Format(
                $"Command arguments are invalid for {typeof(TOptions).Name}.",
                null,
                $"Validation issues ({commandLineValidationResults.Count})",
                commandLineValidationResults.Select(result => result.ErrorMessage),
                [
                    "Fix the listed flag values or missing arguments and retry.",
                    "When a path is shown, it uses the QaaS option object property name."
                ]);
            throw new InvalidConfigurationsException(message);
        }
    }


    /// <summary>
    /// Gets the loaded <see cref="IRunner"/> object based on the provided options 
    /// </summary>
    /// <returns></returns>
    public abstract TRunner GetLoadedRunner();
}
