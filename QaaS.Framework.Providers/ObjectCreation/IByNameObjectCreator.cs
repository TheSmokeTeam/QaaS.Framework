using System.Reflection;
using QaaS.Framework.Providers.CustomExceptions;

namespace QaaS.Framework.Providers.ObjectCreation;

/// <summary>
/// Interface for all class instance creation by naming functionality
/// </summary>
public interface IByNameObjectCreator
{
    /// <summary>
    /// Checks if Type is sub class of T
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <typeparam name="T">The interface or class that the given type is checked to be derived from</typeparam>
    /// <returns>True if type is derived from T false otherwise</returns>
    public bool IsTypeSubClassOfT<T>(Type type);

    /// <summary>
    /// Create instance of sub class of class or interface T according to the class manifestation name, searches for that class
    /// in the given enumerable of assemblies
    /// </summary>
    /// <param name="classManifestationName"> name of the class to initialize as long as its a sub class of T </param>
    /// <param name="assemblies"> The enumerable of assemblies to look for the sub class type in </param>
    /// <param name="constructorArgs">The arguments for the class constructor</param>
    /// <typeparam name="T">The interface or class that the class derived from</typeparam>
    /// <returns>Instance of the class</returns>
    /// <exception cref="UnsupportedSubClassException">Raise when the class name is not sub class of the interface or class</exception>
    public T GetInstanceOfSubClassOfTByNameFromAssemblies<T>(string classManifestationName,
        IEnumerable<Assembly>? assemblies,
        params object[]? constructorArgs);

}