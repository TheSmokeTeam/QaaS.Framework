using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Providers.Providers;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.Hooks;

namespace QaaS.Framework.Providers;

/// <summary>
/// Responsible for loading hooks from a list of hook providers
/// </summary>
/// <typeparam name="THook"> The type of hook in the hook providers </typeparam>
public class HooksFromProvidersLoader<THook>(Context context, IHookProvider<THook> hookProvider)
    where THook : IHook
{
    /// <summary>
    /// Loads and validates all given hooks from their provider
    /// </summary>
    /// <returns> A list of loaded hooks where the key is their name and the value is the initialized hook </returns>
    public IList<KeyValuePair<string, THook>> LoadAndValidate(IEnumerable<HookData<THook>> hooksNames,
        List<ValidationResult> validationResults)
    {
        context.Logger.LogDebug("Starting loading and validation of all hooks of type {HookType}",
            typeof(THook).Name);
        return hooksNames.Select(hookData =>
        {
            THook hook;
            try
            {
                hook = hookProvider.GetSupportedInstanceByName(hookData.Type);
            }
            catch (ArgumentException e)
            {
                context.Logger.LogCritical(
                    "Encountered exception while loading {HookType} instance {InstanceName} - {Exception}",
                    typeof(THook).Name, hookData.Type, e);
                throw;
            }

            var configurationsValidationResults = (hook.LoadAndValidateConfiguration(
                hookData.Configuration) ?? Enumerable.Empty<ValidationResult>()).ToList();
            foreach (var validationResult in configurationsValidationResults)
                validationResult.ErrorMessage = $"In Hook of {typeof(THook).Name} named {hookData.Name} of type" +
                                                $" {hookData.Type} {validationResult.ErrorMessage}";
            validationResults.AddRange(configurationsValidationResults);
            return new KeyValuePair<string, THook>(hookData.Name, hook);
        }).ToList();
    }
}