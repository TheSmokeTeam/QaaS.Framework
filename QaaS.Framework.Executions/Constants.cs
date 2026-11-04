using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace QaaS.Framework.Executions;

/// <summary>
/// Exposes the default loggers used by framework execution infrastructure.
/// </summary>
[Obsolete("Use ExecutionLogging instead.")]
public static class Constants
{
    /// <summary>
    /// Gets the default Serilog logger shared by framework execution infrastructure.
    /// </summary>
    public static Serilog.ILogger DefaultSerilogLogger => ExecutionLogging.DefaultSerilogLogger;

    /// <summary>
    /// Gets the default Microsoft logger shared by framework execution infrastructure.
    /// </summary>
    public static ILogger DefaultLogger => ExecutionLogging.DefaultLogger;
}
