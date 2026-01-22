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
        Type? subClassType = default;
        foreach(var assembly in assemblies ?? Array.Empty<Assembly>())
        {
            try
            {
                subClassType = GetAllSubClassesOfT<T>(assembly).GetValueOrDefault(classManifestationName);
            }
            catch (Exception e)
            {
                logger.LogDebug(
                    "Could not retrieve types from assembly {AssemblyFullName}, skipping it.\n " +
                    "Encountered the following exception when trying to retrieve from it:\n {Exception}", 
                    assembly.FullName, e);
            }
            if (subClassType != default) break;
        }
        // If subClassType is still the default value it means its not available and cannot be found
        if (subClassType == default)
            throw new UnsupportedSubClassException(classManifestationName, typeof(T));
        
        logger.LogDebug("Found subclass of type {TType}, the class's full name is" +
                               " {SubClassFullName}, Initializing it.", typeof(T).Name, subClassType.FullName);
        return (T)Activator.CreateInstance(subClassType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null, constructorArgs, null)!;
    }
    
    /// <summary>
    /// Gets all the sub classes names and types of T
    /// </summary>
    /// <param name="assembly"> which assembly to get the sub classes from, by default checks the assembly the given T belongs to </param>
    /// <typeparam name="T">The interface or class that the class derived from</typeparam>
    /// <returns>A dictionary where the key is the type name and the value is the type itself of all classes derived from T</returns>
    private Dictionary<string, Type> GetAllSubClassesOfT<T>(Assembly? assembly = null) => 
        (assembly ?? Assembly.GetAssembly(typeof(T)) ?? 
            throw new ArgumentException($"Could not find assembly of type {typeof(T)}"))
        .GetTypes()
        .Where(IsTypeSubClassOfT<T>)
        .ToDictionary(type => type.Name, type => type);

}