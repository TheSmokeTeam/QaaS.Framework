using CommandLine;
using QaaS.Framework.Configurations.CustomValidationAttributes;
using Serilog.Events;

namespace QaaS.Framework.Executions.Options;

/// <summary>
/// The base options every executor should have
/// </summary>
public abstract record LoggerOptions 
{
    [Option('l', "logger-level", HelpText = @$"
The logger's level, overrides both the default logger's level ({nameof(LogEventLevel.Information)}) and the level of any logger's configuration given.
All available options (not case sensitive) are: {nameof(LogEventLevel.Verbose)}, {nameof(LogEventLevel.Debug)}, 
{nameof(LogEventLevel.Information)}, {nameof(LogEventLevel.Warning)}, {nameof(LogEventLevel.Error)}, {nameof(LogEventLevel.Fatal)}.")]
    public LogEventLevel? LoggerLevel { get; init; } = null;

    [ValidPath, Option('g', "logger-configuration-file", Default = null,
         HelpText = "Path to a logger's configuration file, will override the default logger's configuration." +
                    " Its level can be overridden by the logger-level flag.")]
    public string? LoggerConfigurationFilePath { get; init; } = null;

    
    [Option("send-logs", HelpText = @"Whether to send the logs to Smoke's logs database", Default = false)]
    public bool SendLogs { get; init; } = false;

    [Option("elastic-uri", Default = null,
        HelpText = "Elasticsearch URI used by the logger sink when send-logs is enabled.")]
    public string? ElasticUri { get; init; } = null;

    [Option("elastic-username", Default = null,
        HelpText = "Optional Elasticsearch username for the logger sink.")]
    public string? ElasticUsername { get; init; } = null;

    [Option("elastic-password", Default = null,
        HelpText = "Optional Elasticsearch password for the logger sink.")]
    public string? ElasticPassword { get; init; } = null;

    [Option("disable-elastic-defaults", Default = false,
        HelpText = "Disables Elastic defaults registered through the runtime defaults provider for this run.")]
    public bool DisableElasticDefaults { get; init; } = false;
}
