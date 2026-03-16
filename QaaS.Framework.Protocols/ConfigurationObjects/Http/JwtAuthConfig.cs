using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Http;

public record JwtAuthConfig
{
    [Required, Description("The JWT secret")]
    public string? Secret { get; set; }

    [RequiredOrNullBasedOnOtherFieldsConfiguration(new[] { nameof(HierarchicalClaims) }, false),
     Description("Custom claims of the JWT")]
    public Dictionary<string, string> Claims { get; set; } = new();

    [MinLength(1),
     RequiredOrNullBasedOnOtherFieldsConfiguration(new[] { nameof(Claims) }, false),
     YamlStringDeserializable(typeof(Dictionary<string, object>)),
     Description($"Custom Hierarchical claims of the JWT, Must be a string in yaml format. " +
                 $"When set to a non null value the {nameof(Claims)} field will be ignored and this will be used instead."),
     DefaultValue(null)]
    public string? HierarchicalClaims { get; set; } = null;

    [Description("The JWT Algorithm algorithm used"), DefaultValue(JwtAlgorithms.HMACSHA256Algorithm)]
    public JwtAlgorithms JwtAlgorithm { get; set; } = JwtAlgorithms.HMACSHA256Algorithm;

    [Description("The authorization scheme to use"), DefaultValue(HttpAuthorizationSchemes.Bearer)]
    public HttpAuthorizationSchemes HttpAuthScheme { get; set; } = HttpAuthorizationSchemes.Bearer;

    [Description($"Whether to build JWT config with claims or send the {nameof(Secret)} value as the auth token"),
     DefaultValue(true)]
    public bool BuildJwtConfig { get; set; } = true;
}
