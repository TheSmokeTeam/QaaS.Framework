using CommandLine;
using CommandLine.Text;

namespace QaaS.Framework.Executions.CommandLineBuilders;

/// <summary>
/// Builds the CLI's HelpText
/// </summary>
public static class HelpTextBuilder
{
    public static HelpText BuildHelpText(ParserResult<object> parserResult)
    {
        var helpText = HelpText.AutoBuild(parserResult, 120);
        helpText.AddPreOptionsLine("Usage:\n dotnet run [Dotnet Parameters] -- [Command] [Values] [Flags]");
        return helpText;
    }
}