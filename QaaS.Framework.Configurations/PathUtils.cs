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
            throw new ArgumentException("Overwrite folders must be local directories.", nameof(directoryPath));

        var resolvedDirectoryPath = Path.IsPathRooted(directoryPath)
            ? directoryPath
            : Path.Combine(Environment.CurrentDirectory, directoryPath);

        if (!Directory.Exists(resolvedDirectoryPath))
            throw new DirectoryNotFoundException(
                $"Overwrite folder '{directoryPath}' was not found. Resolved path: '{resolvedDirectoryPath}'.");

        return Directory.EnumerateFiles(resolvedDirectoryPath, "*", SearchOption.TopDirectoryOnly)
            .Where(filePath =>
                filePath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                filePath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase);
    }
}
