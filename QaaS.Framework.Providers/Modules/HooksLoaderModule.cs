using System.ComponentModel.DataAnnotations;
using Autofac;
using QaaS.Framework.Providers.ObjectCreation;
using QaaS.Framework.Providers.Providers;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.Hooks;

namespace QaaS.Framework.Providers.Modules;

/// <summary>
/// Registers all hooks of type <see cref="THook"/> to the container builder.
/// Requires <see cref="Context"/>,
/// <see cref="IByNameObjectCreator"/>
/// and an IEnumerable of <see cref="HookData{THook}"/> to be registered in the container builder
/// <typeparam name="THook">The type of the hook to register to the container builder</typeparam>
/// <param name="validationResults"> An existing list of validation results that will be expended with all
/// validation results of the registered hooks </param>
/// </summary>
public class HooksLoaderModule<THook>(List<ValidationResult> validationResults): Module where THook: IHook
{
    /// <summary>
    /// All validation results of objects created in this module are loaded to this object after its container builder is resolved
    /// </summary>
    public List<ValidationResult> ValidationResults { get; } = validationResults;


    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<HookProvider<THook>>().As<IHookProvider<THook>>()
            .InstancePerLifetimeScope();
        builder.RegisterType<HooksFromProvidersLoader<THook>>().InstancePerLifetimeScope();
        builder.Register<IComponentContext, IList<KeyValuePair<string, THook>>>(context =>
            context.Resolve<HooksFromProvidersLoader<THook>>()
            .LoadAndValidate(context.Resolve<IEnumerable<HookData<THook>>>(), ValidationResults))
            .InstancePerLifetimeScope();
    }
}