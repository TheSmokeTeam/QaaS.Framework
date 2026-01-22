using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations.ConfigurationProviders;

namespace QaaS.Framework.Configurations.ConfigurationSources;

/// <summary>
/// Supports a configuration source of a YAML file from an http get request's content
/// </summary>
public class HttpGetYamlConfigurationSource: IConfigurationSource
{
    private readonly string _yamlUrl;
    private readonly TimeSpan? _timeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpGetYamlConfigurationSource"/> class
    /// </summary>
    /// <param name="yamlUrl">The url to the YAML file to get</param>
    /// <param name="timeout">The timeout for the HTTP request. If not provided, the default is 100 seconds</param>
    public HttpGetYamlConfigurationSource(string yamlUrl, TimeSpan? timeout = null)
    {
        _yamlUrl = yamlUrl;
        _timeout = timeout;
    }

    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new HttpGetYamlConfigurationProvider(_yamlUrl, _timeout);
    }
}