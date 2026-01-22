using System.Diagnostics.CodeAnalysis;

namespace QaaS.Framework.SDK.MockerObjects.ConfigurationObjects.Ping;

[ExcludeFromCodeCoverage]
public record PingRequest
{
    public string Id { get; set; }
}