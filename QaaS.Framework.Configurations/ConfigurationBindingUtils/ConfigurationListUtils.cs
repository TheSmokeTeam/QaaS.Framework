using System.Collections;

namespace QaaS.Framework.Configurations.ConfigurationBindingUtils;

/// <summary>
/// Utility methods for lists
/// </summary>
internal static class ListUtils
{
    /// <summary>
    /// Get list items type and instance
    /// </summary>
    /// <param name="listType">The list type</param>
    /// <param name="parentPath">The path to the property</param>
    /// <returns>Tuple {list items type, list instance}</returns>
    /// <exception cref="ArgumentException">Thrown when list can't be created from the given type</exception>
    internal static (Type, IList?) GetListItemsTypeAndInstance(Type listType, string parentPath)
    {
        var listItemsType = listType.IsGenericType ?
            listType.GetGenericArguments().FirstOrDefault() :
            listType.GetElementType();

        var listInstance = (IList?)Activator.CreateInstance(typeof(List<>).MakeGenericType(
            listItemsType ?? throw new ArgumentException($"Failed to get list type for {parentPath}")));

        return (listItemsType, listInstance);
    }
    
    /// <summary>
    /// Returns true if the type is a list
    /// </summary>
    internal static bool IsTypeList(this Type type) =>
        type == typeof(IList) ||
        typeof(IList).IsAssignableFrom(type);

}