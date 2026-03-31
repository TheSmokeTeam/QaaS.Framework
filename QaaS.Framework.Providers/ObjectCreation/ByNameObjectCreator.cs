using System.Reflection;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Providers.CustomExceptions;

namespace QaaS.Framework.Providers.ObjectCreation;

/// <summary>
/// Contains functionality for creating an instance of a class by class name  
/// </summary>
public class ByNameObjectCreator(ILogger logger): IByNameObjectCreator
{
    /// <inheritdoc />
    public bool IsTypeSubClassOfT<T>(Type type) => 
        typeof(T).IsAssignableFrom(type) && type is { IsAbstract: false, IsClass: true };

    /// <inheritdoc />
    public T GetInstanceOfSubClassOfTByNameFromAssemblies<T>(string classManifestationName,
        IEnumerable<Assembly>? assemblies,
        params object[]? constructorArgs)
    {
        var subClassType = ResolveSubClassType<T>(classManifestationName, assemblies);

        logger.LogDebug("Found subclass of type {TType}, the class's full name is" +
                               " {SubClassFullName}, Initializing it.", typeof(T).Name, subClassType.FullName);
        return (T)Activator.CreateInstance(subClassType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null, constructorArgs, null)!;
    }

    private Type ResolveSubClassType<T>(string classManifestationName, IEnumerable<Assembly>? assemblies)
    {
        var candidateTypes = GetCandidateTypesByAssembly<T>(assemblies).ToList();

        var fullNameMatches = candidateTypes
            .SelectMany(pair => pair.Types)
            .Where(type => string.Equals(type.FullName, classManifestationName, StringComparison.Ordinal) ||
                           string.Equals(type.AssemblyQualifiedName, classManifestationName, StringComparison.Ordinal))
            .Distinct()
            .ToList();

        if (fullNameMatches.Count == 1)
            return fullNameMatches[0];

        if (fullNameMatches.Count > 1)
            throw new AmbiguousMatchException(CreateAmbiguousTypeMessage(typeof(T), classManifestationName, fullNameMatches));

        foreach (var candidateTypesInAssembly in candidateTypes)
        {
            var simpleNameMatches = candidateTypesInAssembly.Types
                .Where(type => string.Equals(type.Name, classManifestationName, StringComparison.Ordinal))
                .Distinct()
                .ToList();

            if (simpleNameMatches.Count == 1)
                return simpleNameMatches[0];

            if (simpleNameMatches.Count > 1)
                throw new AmbiguousMatchException(
                    CreateAmbiguousTypeMessage(typeof(T), classManifestationName, simpleNameMatches));
        }

        throw new UnsupportedSubClassException(classManifestationName, typeof(T));
    }
    
    /// <summary>
    /// Gets all the sub classes names and types of T
    /// </summary>
    /// <param name="assembly"> which assembly to get the sub classes from, by default checks the assembly the given T belongs to </param>
    /// <typeparam name="T">The interface or class that the class derived from</typeparam>
    /// <returns>All classes derived from T</returns>
    private IEnumerable<Type> GetAllSubClassesOfT<T>(Assembly? assembly = null) =>
        GetLoadableTypes(assembly ?? Assembly.GetAssembly(typeof(T)) ??
            throw new ArgumentException($"Could not find assembly of type {typeof(T)}"))
        .Where(IsTypeSubClassOfT<T>)
        .Distinct();

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException reflectionTypeLoadException)
        {
            return reflectionTypeLoadException.Types.Where(type => type is not null)!;
        }
    }

    private static string CreateAmbiguousTypeMessage(Type requestedBaseType, string classManifestationName,
        IEnumerable<Type> matchingTypes)
        => $"Found multiple subclasses of {requestedBaseType.Name} matching '{classManifestationName}'. " +
           "Use the hook's full type name instead. If the same full type name exists in multiple assemblies, " +
           $"use the assembly-qualified name instead. Candidates:{Environment.NewLine}- " +
           string.Join(
               $"{Environment.NewLine}- ",
               matchingTypes.Select(type => $"{type.FullName} ({type.Assembly.FullName})"));

    private IEnumerable<(Assembly Assembly, IReadOnlyCollection<Type> Types)> GetCandidateTypesByAssembly<T>(
        IEnumerable<Assembly>? assemblies)
    {
        foreach (var assembly in assemblies ?? Array.Empty<Assembly>())
        {
            IReadOnlyCollection<Type> types;
            try
            {
                types = GetAllSubClassesOfT<T>(assembly).ToArray();
            }
            catch (Exception e)
            {
                logger.LogDebug(
                    "Could not retrieve types from assembly {AssemblyFullName}, skipping it.\n " +
                    "Encountered the following exception when trying to retrieve from it:\n {Exception}",
                    assembly.FullName, e);
                continue;
            }

            yield return (assembly, types);
        }
    }

}
