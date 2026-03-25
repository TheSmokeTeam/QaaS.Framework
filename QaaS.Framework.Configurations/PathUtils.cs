namespace QaaS.Framework.Configurations;

/// <summary>
/// Utility functions for handling path strings
/// </summary>
public static class PathUtils
{
    /// <summary>
    /// Checks if a path is an http url
    /// </summary>
    /// <param name="path"> The path to check </param>
    /// <returns> True if the path is an http url and false otherwise </returns>
    public static bool IsPathHttpUrl(string? path)
    {
        return path != null && ( path.StartsWith("http://") || path.StartsWith("https://"));
    }

    /// <summary>
    /// Enumerates YAML files in a local directory in deterministic alphabetical order.
    /// </summary>
    /// <param name="directoryPath">The relative or absolute directory path.</param>
    /// <returns>The matching YAML file paths.</returns>
    public static IEnumerable<string> EnumerateYamlFilesInDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (IsPathHttpUrl(directoryPath))
            throw new ArgumentException(
                DiagnosticMessageFormatter.Format(
                    "Overwrite folders must be local directories.",
                    [$"Configured overwrite folder: {directoryPath}"],
                    null,
                    null,
                    [
                        "Use --with-folders only with a local directory path.",
                        "Use --with-files for individual YAML files."
                    ]),
                nameof(directoryPath));

        var resolvedDirectoryPath = Path.IsPathRooted(directoryPath)
            ? directoryPath
            : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, directoryPath));

        if (!Directory.Exists(resolvedDirectoryPath))
            throw new DirectoryNotFoundException(
                DiagnosticMessageFormatter.Format(
                    "Overwrite folder was not found.",
                    [
                        $"Configured overwrite folder: {directoryPath}",
                        $"Resolved local path: {resolvedDirectoryPath}"
                    ],
                    null,
                    null,
                    [
                        "Provide a valid local directory path in --with-folders or remove the value and retry."
                    ]));

        return Directory.EnumerateFiles(resolvedDirectoryPath, "*", SearchOption.TopDirectoryOnly)
            .Where(filePath =>
                filePath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                filePath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase);
    }
}
