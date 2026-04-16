using System.Collections;
using System.Reflection;

namespace QaaS.Framework.Infrastructure;

/// <summary>
/// Reflection-based helper that produces independent builder copies.
/// </summary>
/// <remarks>
/// <para>
/// The algorithm starts from a shallow <c>MemberwiseClone</c> of the source (copying every public,
/// internal, and private field), then walks the instance fields and replaces mutable containers that
/// the builder owns — <c>T[]</c>, <c>List&lt;T&gt;</c> and <c>Dictionary&lt;K,V&gt;</c> — with fresh
/// copies so the clone can be mutated without affecting the original. Elements that themselves
/// implement <c>ICloneable&lt;&gt;</c> (from any namespace) are cloned recursively via
/// their own <c>Clone()</c> method; other references are shared.
/// </para>
/// <para>
/// This is a best-effort helper for typical builder shapes. Builders with unusual state (captured
/// delegates, non-generic collections, custom init-once fields) should implement <c>Clone()</c>
/// manually instead of delegating here.
/// </para>
/// </remarks>
public static class BuilderCloner
{
    private static readonly MethodInfo MemberwiseCloneMethod =
        typeof(object).GetMethod(
            nameof(MemberwiseClone),
            BindingFlags.Instance | BindingFlags.NonPublic)!;

    public static T DeepClone<T>(T source) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        var clone = (T)MemberwiseCloneMethod.Invoke(source, null)!;
        RebuildOwnedContainers(clone);
        return clone;
    }

    private static void RebuildOwnedContainers(object obj)
    {
        var type = obj.GetType();
        while (type != null && type != typeof(object))
        {
            foreach (var field in type.GetFields(
                         BindingFlags.Instance |
                         BindingFlags.Public |
                         BindingFlags.NonPublic |
                         BindingFlags.DeclaredOnly))
            {
                var value = field.GetValue(obj);
                if (value is null) continue;

                if (TryRebuildContainer(value, out var rebuilt))
                {
                    field.SetValue(obj, rebuilt);
                }
            }
            type = type.BaseType;
        }
    }

    private static bool TryRebuildContainer(object value, out object? rebuilt)
    {
        var valueType = value.GetType();

        if (value is Array array && valueType.GetArrayRank() == 1)
        {
            var elementType = valueType.GetElementType()!;
            var copy = Array.CreateInstance(elementType, array.Length);
            for (var i = 0; i < array.Length; i++)
            {
                var element = array.GetValue(i);
                copy.SetValue(CloneElement(element), i);
            }
            rebuilt = copy;
            return true;
        }

        if (valueType.IsGenericType &&
            valueType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = (IList)Activator.CreateInstance(valueType)!;
            foreach (var element in (IEnumerable)value)
            {
                list.Add(CloneElement(element));
            }
            rebuilt = list;
            return true;
        }

        if (valueType.IsGenericType &&
            valueType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var dict = (IDictionary)Activator.CreateInstance(valueType)!;
            foreach (DictionaryEntry entry in (IDictionary)value)
            {
                dict[entry.Key] = CloneElement(entry.Value);
            }
            rebuilt = dict;
            return true;
        }

        rebuilt = null;
        return false;
    }

    private static object? CloneElement(object? element)
    {
        if (element is null) return null;

        var cloneable = element.GetType().GetInterfaces()
            .FirstOrDefault(IsGenericCloneable);
        if (cloneable != null)
        {
            var cloneMethod = cloneable.GetMethod("Clone", Type.EmptyTypes);
            if (cloneMethod != null)
            {
                return cloneMethod.Invoke(element, null);
            }
        }
        return element;
    }

    private static bool IsGenericCloneable(Type iface)
    {
        if (!iface.IsGenericType) return false;
        var def = iface.GetGenericTypeDefinition();
        return def.Name == "ICloneable`1";
    }
}
