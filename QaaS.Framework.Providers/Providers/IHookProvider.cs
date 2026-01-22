using QaaS.Framework.SDK.Hooks;

namespace QaaS.Framework.Providers.Providers;

/// <summary>
/// Represents a hook provider which is a class that provides hooks that inherit from the interface IHook
/// </summary>
public interface IHookProvider<out THook> where THook : IHook
{
    /// <summary>
    /// Get an item by its name from an enumerable of providers
    /// </summary>
    /// <exception cref="ArgumentException"> If the item was not found in the provider exception is thrown </exception>
    public THook GetSupportedInstanceByName(string instanceName);
}