using JWT.Algorithms;
using QaaS.Framework.Configurations.CustomExceptions;
using QaaS.Framework.Protocols.ConfigurationObjects.Http;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace QaaS.Framework.Protocols.Extentions;

internal static class HttpExtentions
{
    internal static IJwtAlgorithm GetJwtAlgorithmFromJwtEnum(JwtAlgorithms jwtAlgorithm)
    {
        return jwtAlgorithm switch
        {
            JwtAlgorithms.HMACSHA256Algorithm => new HMACSHA256Algorithm(),
            _ => throw new ArgumentOutOfRangeException(nameof(jwtAlgorithm), jwtAlgorithm,
                "Jwt algorithm not supported")
        };
    }

    internal static Dictionary<string, object> GetClaimsFromHierarchicalClaims(string hierarchicalClaims)
    {
        var deserializer = new DeserializerBuilder().Build();
        try
        {
            return deserializer.Deserialize<Dictionary<string, object>>(hierarchicalClaims);
        }
        catch (YamlException e)
        {
            throw new InvalidConfigurationsException($"The HierarchicalClaims string must be in yaml format." +
                                                     $" Encountered the following exception when trying to parse it: {e}");
        }
    }
}