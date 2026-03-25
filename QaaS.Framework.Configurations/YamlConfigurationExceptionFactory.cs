using QaaS.Framework.Configurations.CustomExceptions;
using YamlDotNet.Core;

namespace QaaS.Framework.Configurations;

internal static class YamlConfigurationExceptionFactory
{
    public static Exception CreateLocalFileLoadException(string yamlPath, Exception exception)
    {
        if (exception is CouldNotFindConfigurationException or InvalidConfigurationsException)
        {
            return exception;
        }

        if (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new CouldNotFindConfigurationException(
                DiagnosticMessageFormatter.Format(
                    "YAML configuration file was not found.",
                    [$"Resolved local path: {yamlPath}"],
                    null,
                    null,
                    ["Provide a valid YAML file path and retry."]),
                exception);
        }

        if (TryGetSemanticError(exception, out var semanticError))
        {
            return new InvalidConfigurationsException(
                DiagnosticMessageFormatter.Format(
                    "YAML configuration file is invalid and QaaS cannot continue.",
                    [
                        $"Resolved local path: {yamlPath}",
                        $"Parser location: line {semanticError.Start.Line}, column {semanticError.Start.Column}",
                        $"Parser detail: {semanticError.Message}"
                    ],
                    null,
                    null,
                    [
                        "Fix the YAML syntax at the reported file and location, then retry.",
                        "Parser locations are 1-based line and column numbers."
                    ]),
                exception);
        }

        return new InvalidConfigurationsException(
            DiagnosticMessageFormatter.Format(
                "YAML configuration file could not be loaded.",
                [
                    $"Resolved local path: {yamlPath}",
                    $"Load failure detail: {GetMostRelevantMessage(exception)}"
                ],
                null,
                null,
                ["Fix the file contents or accessibility issue and retry."]),
            exception);
    }

    private static bool TryGetSemanticError(Exception exception, out SemanticErrorException semanticError)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is SemanticErrorException matchedSemanticError)
            {
                semanticError = matchedSemanticError;
                return true;
            }
        }

        semanticError = null!;
        return false;
    }

    private static string GetMostRelevantMessage(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                return current.Message;
            }
        }

        return exception.GetType().Name;
    }
}
