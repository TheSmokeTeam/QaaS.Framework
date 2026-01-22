using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Framework.SDK.ConfigurationObjects;

public record StatusCodeConfiguration
{
    [Required, Description("Response Status Code")]
    public int StatusCode { get; set; }   
}