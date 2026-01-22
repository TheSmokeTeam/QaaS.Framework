using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace QaaS.Framework.SDK.MockerObjects.ConfigurationObjects.Command;

[ExcludeFromCodeCoverage]
public record ChangeActionStub
{
    [Required, Description("The Action's name that is being changed")]
    public string? ActionName { get; init; }
    
    [Required, Description("The Stub's Name attached to the action")]
    public string? StubName { get; init; }
}