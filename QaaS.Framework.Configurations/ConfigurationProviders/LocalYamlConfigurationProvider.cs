using NetEscapades.Configuration.Yaml;

namespace QaaS.Framework.Configurations.ConfigurationProviders;

/// <summary>
/// Provides configuration from a local YAML file while surfacing QaaS-specific diagnostics on failures.
/// </summary>
public class LocalYamlConfigurationProvider : YamlConfigurationProvider
{
    private readonly string _yamlPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalYamlConfigurationProvider" /> class.
    /// </summary>
    /// <param name="yamlPath">The resolved path to the YAML file.</param>
    public LocalYamlConfigurationProvider(string yamlPath)
        : base(new YamlConfigurationSource())
    {
        _yamlPath = yamlPath;
    }

    /// <summary>
    /// Loads the configuration from the local YAML file.
    /// </summary>
    public override void Load()
    {
        try
        {
            using var stream = File.OpenRead(_yamlPath);
            base.Load(stream);
        }
        catch (Exception exception)
        {
            throw YamlConfigurationExceptionFactory.CreateLocalFileLoadException(_yamlPath, exception);
        }
    }
}
