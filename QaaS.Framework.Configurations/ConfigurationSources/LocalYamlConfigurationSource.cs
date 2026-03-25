using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations.ConfigurationProviders;

namespace QaaS.Framework.Configurations.ConfigurationSources;

/// <summary>
/// Represents a local YAML configuration source that produces QaaS-aware diagnostics.
/// </summary>
public class LocalYamlConfigurationSource : IConfigurationSource
{
    private readonly string _yamlPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalYamlConfigurationSource" /> class.
    /// </summary>
    /// <param name="yamlPath">The resolved path to the YAML file.</param>
    public LocalYamlConfigurationSource(string yamlPath)
    {
        _yamlPath = yamlPath;
    }

    /// <summary>
    /// Builds the provider for the configured local YAML file.
    /// </summary>
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new LocalYamlConfigurationProvider(_yamlPath);
    }
}
