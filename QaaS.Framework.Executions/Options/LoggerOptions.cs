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

    
    [Option("send-logs",HelpText =@"Weather to send the logs to REDA's logs database")]
    public bool SendLogs { get; init; } = true;
}