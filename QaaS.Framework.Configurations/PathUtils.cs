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
}