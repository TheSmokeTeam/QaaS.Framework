using CommandLine;

namespace QaaS.Framework.Executions.CommandLineBuilders;

/// <summary>
/// Builds the CLI's Parser 
/// </summary>
public static class ParserBuilder
{
    public static Parser BuildParser()
    {
        return new Parser(settings =>
        {
            settings.CaseSensitive = false;
            settings.CaseInsensitiveEnumValues = true;
            settings.HelpWriter = null;
            settings.AllowMultiInstance = true;
            settings.MaximumDisplayWidth = 20_000;
            settings.CaseInsensitiveEnumValues = true;
            settings.CaseSensitive = false;
        });
    }
}