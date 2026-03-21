using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace QaaS.Framework.Executions;

/// <summary>
/// Backward-compatible surface for consumers that still reference the legacy execution constants API.
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
