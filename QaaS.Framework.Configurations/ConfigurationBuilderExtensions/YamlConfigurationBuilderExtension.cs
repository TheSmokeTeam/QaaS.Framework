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
    /// Adds the <see cref="HttpGetYamlConfigurationSource"/> configuration source to the
    /// <see cref="IConfigurationBuilder"/>
    /// </summary>
    /// <param name="builder">The <see cref="IConfigurationBuilder"/> to add to</param>
    /// <param name="yamlUrl">The url to the YAML file to get</param>
    /// <param name="timeout">The timeout for the HTTP request. If not provided, the default is 100 seconds</param>
    /// <returns>The <see cref="IConfigurationBuilder"/></returns>
    public static IConfigurationBuilder AddYamlFromHttpGet(this IConfigurationBuilder builder,
        string yamlUrl, TimeSpan? timeout = null)
    {
        return builder.Add(new HttpGetYamlConfigurationSource(yamlUrl, timeout));
    }
    
    /// <summary>
    /// Adds a YAML to the <see cref="IConfigurationBuilder"/> with
    /// <see cref="ConfigurationCollapseParser.CollapseShiftLeftArrowsInConfiguration"/> support,
    /// if the yaml starts with http:// or https:// adds it
    /// using <see cref="AddYamlFromHttpGet"/> else adds it using the `AddYamlFile`
    /// </summary>
    /// <param name="builder">The <see cref="IConfigurationBuilder"/> to add to</param>
    /// <param name="yamlPath">The path to the yaml to add (can be Url or Path)</param>
    /// <returns>The <see cref="IConfigurationBuilder"/></returns>
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