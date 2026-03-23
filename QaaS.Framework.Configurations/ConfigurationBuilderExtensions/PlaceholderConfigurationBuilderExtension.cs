using Microsoft.Extensions.Configuration;

namespace QaaS.Framework.Configurations.ConfigurationBuilderExtensions;

/// <summary>
/// Extension class used for parsing placeholders for IConfigurationBuilder
/// </summary>
public static class PlaceholderConfigurationBuilderExtension
{
    /// <summary>
    /// Adds the placeholder-resolving configuration source to the configuration builder.
    /// </summary>
    /// <remarks>
    /// Call this extension before building IConfiguration when placeholder expansion should be applied as part of the configuration pipeline.
    /// </remarks>
    /// <qaas-docs group="Configuration" subgroup="Placeholders" />
    public static IConfigurationBuilder AddPlaceholderResolver(this IConfigurationBuilder configurationBuilder)
    {
        var configuration = configurationBuilder.Build();
        var placeholderParser = new ConfigurationPlaceholderParser(configuration);
        return new ConfigurationBuilder().AddConfiguration(placeholderParser.ResolvePlaceholders());
    }
}
