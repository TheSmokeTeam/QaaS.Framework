using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations;
using QaaS.Framework.Configurations.ConfigurationBindingUtils;

namespace QaaS.Framework.SDK.ContextObjects;

public static class Bind
{
    public static TObject BindFromContext<TObject>(Context context, List<ValidationResult> validationResults, BinderOptions binderOptions) where TObject : new()
    {
        
        // Load configuration to c# object
        var configurationObject = context.RootConfiguration.BindToObject<TObject>(binderOptions,
            context.Logger);

        // Reload c# configuration object's all default configured properties to rootConfiguration 
        context.SetRootConfiguration(
            context.RootConfiguration.BindConfigurationObjectToIConfiguration(configurationObject));

        // Validate loaded configuration
        ValidationUtils.TryValidateObjectRecursive(configurationObject, validationResults);

        return configurationObject;
    }
    
}