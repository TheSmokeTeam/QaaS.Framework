using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using QaaS.Framework.SDK.ConfigurationObjects;

namespace QaaS.Framework.SDK.MockerObjects.ConfigurationObjects.Ping;

[ExcludeFromCodeCoverage]
public record PingResponse
{ 
    public string Id { get; set; }
    public string ServerName { get; set; }
    public string ServerInstanceId { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InputOutputState ServerInputOutputState { get; set; }
}