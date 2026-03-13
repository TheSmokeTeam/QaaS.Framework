using NetEscapades.Configuration.Yaml;
using QaaS.Framework.Configurations.CustomExceptions;

namespace QaaS.Framework.Configurations.ConfigurationProviders;

/// <summary>
/// Provides configuration from a YAML file from an http get request's content
/// </summary>
public class HttpGetYamlConfigurationProvider: YamlConfigurationProvider
{
    private const uint DefaultTimeoutSeconds = 100;
    
    private readonly string _yamlUrl;
    private readonly TimeSpan _timeout;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="HttpGetYamlConfigurationProvider"/> class
    /// </summary>
    /// <param name="yamlUrl">The url to the YAML file to get</param>
    /// <param name="timeout">The timeout for the HTTP request. If not provided,
    /// the default is <see cref="DefaultTimeoutSeconds"/> </param>
    public HttpGetYamlConfigurationProvider(string yamlUrl, TimeSpan? timeout = null)
        : base(new YamlConfigurationSource())
    {
        _yamlUrl = yamlUrl;
        _timeout = timeout ?? TimeSpan.FromSeconds(DefaultTimeoutSeconds);
    }

    /// <summary>
    /// Loads the configuration from the YAML content at a url using http get
    /// </summary>
    public override void Load()
    {
        using var httpClient = new HttpClient { Timeout = _timeout };
        try
        {
            using var response = httpClient.GetStreamAsync(_yamlUrl).GetAwaiter().GetResult();
            base.Load(response);
        }
        catch (Exception e)
        {
            throw new CouldNotFindConfigurationException(
                $"Could not find valid yaml configuration in response content" +
                $" when executing http get on url {_yamlUrl}", e);
        }
    }
}
