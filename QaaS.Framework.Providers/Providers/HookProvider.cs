using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
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
    private readonly Lock _hookTypeCacheLock = new();
    private readonly IByNameObjectCreator _objectCreator;
    // Pre-populated hook-type list for the eager-resolution path. Production constructs with an
    // empty array so resolution always takes the lazy per-assembly path; tests overwrite this
    // field via reflection to exercise the eager path (see ProvidersBehaviorTests / Coverage).
    private readonly Type[] _supportedHookTypes;
    private readonly Dictionary<string, Type[]> _supportedHookTypesByAssembly = new(StringComparer.Ordinal);

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="context"> The context to initialize hooks with </param>
    /// <param name="objectCreator"> The object creator used to create hooks </param>
    public HookProvider(Context context, IByNameObjectCreator objectCreator)
    {
        _context = context;
        _objectCreator = objectCreator;
        _hookAssemblies = GetHookAssemblies().ToArray();
        _supportedHookTypes = [];
    }

    private const string QaasAssemblyPrefix = "QaaS.";

    private static bool NameReachesQaas(string? name) =>
        name is not null && name.StartsWith(QaasAssemblyPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Decides whether a bin-folder DLL is worth Assembly.LoadFrom-ing by walking its AssemblyRef
    /// table transitively through other DLLs in the same folder. Plugins that reach QaaS only
    /// through a corporate base library (MyCorp.Plugin -> MyCorp.Hooks -> QaaS.*) are still
    /// included because the walk follows the corporate library's own references.
    ///
    /// Bounded by the number of DLLs in the folder; each is read at most once. On any IO/metadata
    /// failure for the ROOT assembly we return false — an unreadable DLL can't safely host a hook
    /// anyway. Failures for intermediate DLLs in the walk are tolerated so an unreadable corporate
    /// library doesn't silently disable an otherwise-valid plugin chain.
    /// </summary>
    private static bool CouldContainHooks(
        string assemblyPath,
        string? selfName,
        IReadOnlyDictionary<string, string> dllsByName) =>
        CouldContainHooksCore(assemblyPath, selfName, dllsByName, ReadAssemblyReferenceNames);

    /// <summary>
    /// Pure algorithm split from the IO so unit tests can drive it with synthetic reference graphs.
    /// </summary>
    internal static bool CouldContainHooksCore(
        string rootPath,
        string? rootName,
        IReadOnlyDictionary<string, string> dllsByName,
        Func<string, IReadOnlyList<string>?> readReferenceNames)
    {
        if (NameReachesQaas(rootName)) return true;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootPath };
        var queue = new Queue<string>();
        queue.Enqueue(rootPath);

        while (queue.Count > 0)
        {
            var currentPath = queue.Dequeue();
            var referenceNames = readReferenceNames(currentPath);
            if (referenceNames is null)
            {
                if (currentPath == rootPath) return false;
                continue;
            }

            foreach (var referenceName in referenceNames)
            {
                if (NameReachesQaas(referenceName)) return true;
                if (dllsByName.TryGetValue(referenceName, out var referencePath) && visited.Add(referencePath))
                    queue.Enqueue(referencePath);
            }
        }

        return false;
    }

    private static IReadOnlyList<string>? ReadAssemblyReferenceNames(string assemblyPath)
    {
        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata) return [];
            var reader = pe.GetMetadataReader();
            return reader.AssemblyReferences
                .Select(handle => reader.GetString(reader.GetAssemblyReference(handle).Name))
                .ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<Assembly> GetHookAssemblies()
    {
        var assemblies = new Dictionary<string, Assembly>(StringComparer.Ordinal);

        AddAssembly(assemblies, Assembly.GetEntryAssembly());

        foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
            AddAssembly(assemblies, loadedAssembly);

        var binDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var dllPaths = Directory.GetFiles(binDirectory, "*.dll");

        // Pre-index the bin folder by assembly simple name so CouldContainHooks can resolve
        // referenced DLLs without scanning the directory on every reference lookup.
        var dllsByName = new Dictionary<string, string>(dllPaths.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var dllPath in dllPaths)
            dllsByName[Path.GetFileNameWithoutExtension(dllPath)] = dllPath;

        foreach (var assemblyPath in dllPaths)
        {
            try
            {
                var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                if (assemblies.ContainsKey(assemblyName.FullName ?? assemblyName.Name!))
                    continue;

                if (!CouldContainHooks(assemblyPath, assemblyName.Name, dllsByName))
                    continue;

                AddAssembly(assemblies, Assembly.LoadFrom(assemblyPath));
            }
            catch
            {
                // ignore broken/unloadable binaries; debug details are logged when probing types per assembly.
            }
        }

        return assemblies.Values
            .OrderBy(GetAssemblyPriority)
            .ThenBy(assembly => assembly.FullName ?? assembly.GetName().Name, StringComparer.OrdinalIgnoreCase);
    }

    private static int GetAssemblyPriority(Assembly assembly)
    {
        var assemblyName = assembly.GetName().Name ?? string.Empty;
        if (assemblyName.Contains("QaaS", StringComparison.OrdinalIgnoreCase))
            return 0;
        if (assemblyName.Contains("Common", StringComparison.OrdinalIgnoreCase))
            return 1;
        return 2;
    }

    private static void AddAssembly(IDictionary<string, Assembly> assemblies, Assembly? assembly)
    {
        if (assembly is null || assembly.IsDynamic) return;

        var key = assembly.FullName ?? assembly.GetName().Name;
        if (string.IsNullOrWhiteSpace(key) || assemblies.ContainsKey(key)) return;
        assemblies[key] = assembly;
    }

    private Type[] GetSupportedHookTypesFromAssembly(Assembly assembly)
    {
        var assemblyKey = assembly.FullName ?? assembly.GetName().Name ?? assembly.ToString();

        lock (_hookTypeCacheLock)
        {
            if (_supportedHookTypesByAssembly.TryGetValue(assemblyKey, out var cachedTypes))
                return cachedTypes;
        }

        Type[] loadableTypes;
        try
        {
            loadableTypes = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException reflectionTypeLoadException)
        {
            loadableTypes = reflectionTypeLoadException.Types.Where(type => type is not null).ToArray()!;
            _context.Logger.LogDebug(
                "Partially loaded assembly {AssemblyFullName} while searching for {HookType} hooks. " +
                "Continuing with {ResolvedTypeCount} loadable types.",
                assembly.FullName, typeof(THook).FullName, loadableTypes.Length);
        }
        catch (Exception e)
        {
            _context.Logger.LogDebug(
                "Could not search assembly {AssemblyFullName} for {HookType} hooks, skipping it.\n " +
                "Encountered the following exception when searching it:\n {Exception}",
                assembly.FullName, typeof(THook).FullName, e);
            loadableTypes = [];
        }

        var supportedTypes = loadableTypes.Where(_objectCreator.IsTypeSubClassOfT<THook>).ToArray();
        lock (_hookTypeCacheLock)
        {
            _supportedHookTypesByAssembly[assemblyKey] = supportedTypes;
        }

        return supportedTypes;
    }

    private Type ResolveSupportedHookType(string instanceName)
    {
        if (_supportedHookTypes.Length == 0)
            return ResolveSupportedHookTypeLazily(instanceName);

        var fullNameMatches = _supportedHookTypes
            .Where(type => string.Equals(type.FullName, instanceName, StringComparison.Ordinal) ||
                           string.Equals(type.AssemblyQualifiedName, instanceName, StringComparison.Ordinal))
            .Distinct()
            .ToList();

        if (fullNameMatches.Count == 1)
            return fullNameMatches[0];

        if (fullNameMatches.Count > 1)
            throw new ArgumentException(
                $"Found multiple {typeof(THook).Name} hook instances with the exact type name {instanceName}. " +
                "Use the hook's assembly-qualified name instead." +
                $"\n- {string.Join("\n- ", fullNameMatches.Select(type => $"{type.FullName} ({type.Assembly.FullName})"))}");

        var simpleNameMatches = _supportedHookTypes
            .Where(type => string.Equals(type.Name, instanceName, StringComparison.Ordinal))
            .Distinct()
            .ToList();

        foreach (var hookAssembly in _hookAssemblies)
        {
            var simpleNameMatchesInAssembly = simpleNameMatches
                .Where(type => type.Assembly == hookAssembly)
                .ToList();

            if (simpleNameMatchesInAssembly.Count == 1)
            {
                if (simpleNameMatches.Count > 1)
                {
                    _context.Logger.LogInformation(
                        "Found multiple {HookType} hook instances named {InstanceName}. Resolving to {ResolvedHookType} " +
                        "from assembly {AssemblyName} because it appears first in hook discovery order. Candidates:{CandidateList}",
                        typeof(THook).Name,
                        instanceName,
                        simpleNameMatchesInAssembly[0].FullName,
                        hookAssembly.FullName,
                        $"{Environment.NewLine}- " +
                        string.Join(
                            $"{Environment.NewLine}- ",
                            simpleNameMatches.Select(type => $"{type.FullName} ({type.Assembly.FullName})")));
                }

                return simpleNameMatchesInAssembly[0];
            }

            if (simpleNameMatchesInAssembly.Count > 1)
                throw new ArgumentException(
                    $"Found multiple {typeof(THook).Name} hook instances named {instanceName} in assembly {hookAssembly.FullName}. " +
                    "Use the hook's full type name instead." +
                    $"\n- {string.Join("\n- ", simpleNameMatchesInAssembly.Select(type => type.FullName))}");
        }

        return simpleNameMatches.Count switch
        {
            0 => throw new ArgumentException($"{typeof(THook).Name} hook instance {instanceName} " +
                                             "not found in any of the provided assemblies." +
                                             $"\n- {string.Join("\n- ", _hookAssemblies.Select(asm => asm.FullName))}"),
            _ => throw new ArgumentException(
                $"Found multiple {typeof(THook).Name} hook instances named {instanceName}. " +
                "Use the hook's full type name instead." +
                $"\n- {string.Join("\n- ", simpleNameMatches.Select(type => type.FullName))}")
        };
    }

    private Type ResolveSupportedHookTypeLazily(string instanceName)
    {
        var isExactTypeName = instanceName.Contains('.', StringComparison.Ordinal) ||
                              instanceName.Contains(',', StringComparison.Ordinal);
        if (isExactTypeName)
        {
            Type? fullNameMatch = null;
            foreach (var hookAssembly in _hookAssemblies)
            {
                var fullNameMatchesInAssembly = GetSupportedHookTypesFromAssembly(hookAssembly)
                    .Where(type => string.Equals(type.FullName, instanceName, StringComparison.Ordinal) ||
                                   string.Equals(type.AssemblyQualifiedName, instanceName, StringComparison.Ordinal))
                    .Distinct()
                    .ToList();

                if (fullNameMatchesInAssembly.Count > 1)
                    throw new ArgumentException(
                        $"Found multiple {typeof(THook).Name} hook instances with the exact type name {instanceName}. " +
                        "Use the hook's assembly-qualified name instead." +
                        $"\n- {string.Join("\n- ", fullNameMatchesInAssembly.Select(type => $"{type.FullName} ({type.Assembly.FullName})"))}");

                if (fullNameMatchesInAssembly.Count == 1)
                {
                    if (fullNameMatch is not null)
                        throw new ArgumentException(
                            $"Found multiple {typeof(THook).Name} hook instances with the exact type name {instanceName}. " +
                            "Use the hook's assembly-qualified name instead." +
                            $"\n- {fullNameMatch.FullName} ({fullNameMatch.Assembly.FullName})" +
                            $"\n- {fullNameMatchesInAssembly[0].FullName} ({fullNameMatchesInAssembly[0].Assembly.FullName})");

                    fullNameMatch = fullNameMatchesInAssembly[0];
                }
            }

            if (fullNameMatch is not null)
                return fullNameMatch;
        }

        var simpleNameMatches = new List<Type>();
        foreach (var hookAssembly in _hookAssemblies)
        {
            var simpleNameMatchesInAssembly = GetSupportedHookTypesFromAssembly(hookAssembly)
                .Where(type => string.Equals(type.Name, instanceName, StringComparison.Ordinal))
                .Distinct()
                .ToList();

            if (simpleNameMatchesInAssembly.Count > 1)
                throw new ArgumentException(
                    $"Found multiple {typeof(THook).Name} hook instances named {instanceName} in assembly {hookAssembly.FullName}. " +
                    "Use the hook's full type name instead." +
                    $"\n- {string.Join("\n- ", simpleNameMatchesInAssembly.Select(type => type.FullName))}");

            if (simpleNameMatchesInAssembly.Count == 1)
                simpleNameMatches.Add(simpleNameMatchesInAssembly[0]);
        }

        if (simpleNameMatches.Count == 1)
            return simpleNameMatches[0];

        if (simpleNameMatches.Count > 1)
        {
            var resolvedType = simpleNameMatches[0];
            _context.Logger.LogInformation(
                "Found multiple {HookType} hook instances named {InstanceName}. Resolving to {ResolvedHookType} " +
                "from assembly {AssemblyName} because it appears first in hook discovery order. Candidates:{CandidateList}",
                typeof(THook).Name,
                instanceName,
                resolvedType.FullName,
                resolvedType.Assembly.FullName,
                $"{Environment.NewLine}- " +
                string.Join(
                    $"{Environment.NewLine}- ",
                    simpleNameMatches.Select(type => $"{type.FullName} ({type.Assembly.FullName})")));
            return resolvedType;
        }

        throw new ArgumentException($"{typeof(THook).Name} hook instance {instanceName} " +
                                     "not found in any of the provided assemblies." +
                                     $"\n- {string.Join("\n- ", _hookAssemblies.Select(asm => asm.FullName))}");
    }

    private THook GetInstanceFromResolvedType(Type hookType)
    {
        var hookInstance = _objectCreator.GetInstanceOfSubClassOfTByNameFromAssemblies<THook>(
            hookType.FullName!,
            [hookType.Assembly]);
        hookInstance.Context = _context;
        return hookInstance;
    }

    /// <inheritdoc />
    public THook GetSupportedInstanceByName(string instanceName)
    {
        _context.Logger.LogDebug("Looking for {HookType} hook instance {InstanceName} in provided assemblies"
            , typeof(THook).Name, instanceName);
        var hookType = ResolveSupportedHookType(instanceName);
        _context.Logger.LogInformation("Found {HookType} hook instance {InstanceName} in provided assembly {AssemblyName}",
            typeof(THook).Name, instanceName, hookType.Assembly.FullName);
        return GetInstanceFromResolvedType(hookType);
    }
}
