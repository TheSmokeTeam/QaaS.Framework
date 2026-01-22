using Microsoft.Extensions.Configuration;
using QaaS.Framework.SDK.Hooks;

namespace QaaS.Framework.Providers;

/// <summary>
/// All relevant metadata about a hook required to load and validate it from a provider, the type of the hook is also
/// part of the relevant metadata
/// </summary>
public record HookData<THook> where THook: IHook
{
    public string Name { get; init; } = null!;
    
    public string Type { get; init; } = null!;
    
    public IConfiguration Configuration { get; init; } = null!;
}