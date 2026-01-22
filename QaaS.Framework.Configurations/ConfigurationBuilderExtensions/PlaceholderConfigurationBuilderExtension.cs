using Microsoft.Extensions.Configuration;

namespace QaaS.Framework.Configurations.ConfigurationBuilderExtensions;

/// <summary>
/// Extension class used for parsing placeholders for IConfigurationBuilder
/// </summary>
public static class PlaceholderConfigurationBuilderExtension
{
    /// <summary>
    /// Resolves placeholders in the configurationBuilder
    /// </summary>
    /// <param name="configurationBuilder">The <see cref="IConfigurationBuilder"/> with unresolved placeholders</param>
    /// <returns>The <see cref="IConfigurationBuilder"/> with resolved placeholders</returns>
    public static IConfigurationBuilder AddPlaceholderResolver(this IConfigurationBuilder configurationBuilder)
    {
        var configuration = configurationBuilder.Build();
        var placeholderParser = new ConfigurationPlaceholderParser(configuration);
        return new ConfigurationBuilder().AddConfiguration(placeholderParser.ResolvePlaceholders());
    }
}