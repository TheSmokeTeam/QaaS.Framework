global using NUnit.Framework;
using Serilog;
using Serilog.Extensions.Logging;

public static class Globals
{
    public static readonly Microsoft.Extensions.Logging.ILogger Logger = new SerilogLoggerFactory(
        new LoggerConfiguration().MinimumLevel.Debug()
            .WriteTo.NUnitOutput()
            .CreateLogger()).CreateLogger("TestsLogger");
}