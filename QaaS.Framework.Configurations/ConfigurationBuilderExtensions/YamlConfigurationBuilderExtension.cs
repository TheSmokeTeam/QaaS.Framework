using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations;
using QaaS.Framework.Configurations.ConfigurationSources;

namespace QaaS.Framework.Configurations.ConfigurationBuilderExtensions;

/// <summary>
/// Provides extension methods related to YAML files from an http get request's content for the
/// <see cref="IConfigurationBuilder"/>
/// </summary>
public static class YamlConfigurationBuilderExtension
{
    /// <summary>
    /// Adds a YAML configuration source that is loaded through HTTP GET.
    /// </summary>
    /// <remarks>
    /// Call this extension during configuration bootstrap when YAML should be loaded remotely instead of from the local file system.
    /// </remarks>
    /// <qaas-docs group="Configuration" subgroup="YAML" />
    public static IConfigurationBuilder AddYamlFromHttpGet(this IConfigurationBuilder builder,
        string yamlUrl, TimeSpan? timeout = null)
    {
        return builder.Add(new HttpGetYamlConfigurationSource(yamlUrl, timeout));
    }
    
    /// <summary>
    /// Adds a YAML configuration source from a local file path or URL.
    /// </summary>
    /// <remarks>
    /// Call this extension during configuration bootstrap so YAML sources go through the same QaaS-aware loading path for files and remote URLs.
    /// </remarks>
    /// <qaas-docs group="Configuration" subgroup="YAML" />
    public static IConfigurationBuilder AddYaml(this IConfigurationBuilder builder,
        string yamlPath)
    {
        var correctYamlPath = GetYamlCorrectPath(yamlPath);
        return PathUtils.IsPathHttpUrl(correctYamlPath) 
            ? builder.AddYamlFromHttpGet(correctYamlPath)
            // Configured to `optional: false` so if yaml is not found throws exception
            : builder.AddYamlFile(correctYamlPath, optional: false);
    }
    
    /// <summary>
    /// If path is http based return it as is else get its full path
    /// </summary>
    /// <param name="yamlPath"> The path to check </param>
    /// <returns> The correct path to the yaml </returns>
    private static string GetYamlCorrectPath(string yamlPath) =>
         PathUtils.IsPathHttpUrl(yamlPath)
            ? yamlPath
            : Path.Combine(Environment.CurrentDirectory, yamlPath);
}
