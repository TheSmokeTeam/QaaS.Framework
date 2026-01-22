using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Providers.ObjectCreation;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.Hooks;

namespace QaaS.Framework.Providers.Providers;

/// <inheritdoc />
public class HookProvider<THook> : IHookProvider<THook> where THook : IHook
{
    private readonly Context _context;
    private readonly Assembly[] _hookAssemblies;
    private readonly IByNameObjectCreator _objectCreator;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="context"> The context to initialize hooks with </param>
    /// <param name="objectCreator"> The object creator used to create hooks </param>
    public HookProvider(Context context, IByNameObjectCreator objectCreator)
    {
        _context = context;
        _objectCreator = objectCreator;
        _hookAssemblies = new[]
            {
                Assembly.GetEntryAssembly() ?? throw new ArgumentException(
                    "Could not find Entry Assembly, was called from an unmanaged code")
            }
            .Concat(GetAllAssembliesDirectlyReferencedByProject()).ToArray();
    }

    private static IEnumerable<Assembly> GetAllAssembliesDirectlyReferencedByProject() =>
        Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll").Select(Assembly.LoadFrom);

    /// <summary>
    /// Whether the given hook (by name) is supported in the given assembly
    /// </summary>
    /// <param name="name"> The name of the hook to check for support of </param>
    /// <param name="assembly"> The assembly to look for the given hook </param>
    /// <returns> True if the hook is supported and false otherwise </returns>
    private bool IsSupportedInAssembly(string name, Assembly assembly) =>
        assembly.GetTypes().Any(type => type.Name == name &&
                                        _objectCreator.IsTypeSubClassOfT<THook>(type));

    /// <summary>
    /// Whether the given hook (by name) is supported by the providers
    /// </summary>
    /// <param name="name"> The name of the hook to check for support of </param>
    /// <returns> True if the hook is supported and false otherwise </returns>
    private bool IsSupported(string name)
    {
        var isSupported = false;
        foreach (var assembly in _hookAssemblies)
        {
            try
            {
                if (!IsSupportedInAssembly(name, assembly))
                    continue;
                _context.Logger.LogInformation("Found {HookType} hook instance " +
                                               "{InstanceName} in provided assembly {AssemblyName}"
                    , typeof(THook).Name, name, assembly.FullName);
                isSupported = true;
                break;
            }
            catch (Exception e)
            {
                _context.Logger.LogDebug(
                    "Could not search assembly {AssemblyFullName} for {HookType} hooks, skipping it.\n " +
                    "Encountered the following exception when searching it:\n {Exception}",
                    assembly.FullName, typeof(THook).FullName, e);
            }
        }

        return isSupported;
    }

    /// <summary>
    /// Get initialized instance of the hook by name
    /// </summary>
    /// <param name="instanceName"> The name of the instance to initialize </param>
    /// <returns> The instance of the hook name initialized </returns>
    private THook GetInstanceByName(string instanceName)
    {
        var hookInstance = _objectCreator.GetInstanceOfSubClassOfTByNameFromAssemblies<THook>(instanceName,
            _hookAssemblies);
        hookInstance.Context = _context;
        return hookInstance;
    }

    /// <inheritdoc />
    public THook GetSupportedInstanceByName(string instanceName)
    {
        _context.Logger.LogDebug("Looking for {HookType} hook instance {InstanceName} in provided assemblies"
            , typeof(THook).Name, instanceName);
        if (IsSupported(instanceName)) return GetInstanceByName(instanceName);
        throw new ArgumentException($"{typeof(THook).Name} hook instance {instanceName} " +
                                    $"not found in any of the following provided assemblies:" +
                                    $"\n- {string.Join("\n- ", _hookAssemblies.Select(asm => asm.FullName))}");
    }
}