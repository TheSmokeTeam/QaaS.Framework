using System.Collections;
using System.Reflection;

namespace QaaS.Framework.Infrastructure;

/// <summary>
/// Reflection-based helper that produces independent builder copies by recursively
/// deep-cloning every mutable reference reachable from the source.
/// </summary>
/// <remarks>
/// <para>
/// The algorithm walks the source's object graph, MemberwiseCloning each non-immutable
/// reference and then recursing into its fields. Arrays, <see cref="List{T}"/>,
/// <see cref="Dictionary{TKey,TValue}"/>, and <see cref="HashSet{T}"/> are rebuilt
/// with cloned elements. A reference-equality visited map preserves shared-subgraph
/// identity and prevents cycles. Primitives, enums, strings, and well-known opaque
/// system types (Type, MemberInfo, Assembly, Delegate, ...) are treated as values
/// and shared as-is.
/// </para>
/// <para>
/// Builders whose state is intentionally shared with external owners (for example a
/// user-supplied <c>ILogger</c> that must remain the same instance) should implement
/// <c>Clone()</c> manually instead of delegating here.
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
        var visited = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
        return (T)DeepCloneInternal(source, visited)!;
    }

    private static object? DeepCloneInternal(object? value, Dictionary<object, object> visited)
    {
        if (value is null) return null;

        var type = value.GetType();
        if (IsImmutableOrOpaque(type)) return value;

        if (visited.TryGetValue(value, out var existing)) return existing;

        if (value is Array array && type.GetArrayRank() == 1)
        {
            var elementType = type.GetElementType()!;
            var copy = Array.CreateInstance(elementType, array.Length);
            visited[value] = copy;
            for (var i = 0; i < array.Length; i++)
            {
                copy.SetValue(DeepCloneInternal(array.GetValue(i), visited), i);
            }
            return copy;
        }

        if (type.IsGenericType)
        {
            var genericDefinition = type.GetGenericTypeDefinition();

            if (genericDefinition == typeof(List<>))
            {
                var list = (IList)Activator.CreateInstance(type)!;
                visited[value] = list;
                foreach (var element in (IEnumerable)value)
                {
                    list.Add(DeepCloneInternal(element, visited));
                }
                return list;
            }

            if (genericDefinition == typeof(Dictionary<,>))
            {
                var dictionary = (IDictionary)Activator.CreateInstance(type)!;
                visited[value] = dictionary;
                foreach (DictionaryEntry entry in (IDictionary)value)
                {
                    dictionary[entry.Key] = DeepCloneInternal(entry.Value, visited);
                }
                return dictionary;
            }

            if (genericDefinition == typeof(HashSet<>))
            {
                var set = Activator.CreateInstance(type)!;
                var addMethod = type.GetMethod("Add", [type.GetGenericArguments()[0]])!;
                visited[value] = set;
                foreach (var element in (IEnumerable)value)
                {
                    addMethod.Invoke(set, [DeepCloneInternal(element, visited)]);
                }
                return set;
            }
        }

        var clone = MemberwiseCloneMethod.Invoke(value, null)!;
        visited[value] = clone;

        var currentType = type;
        while (currentType != null && currentType != typeof(object))
        {
            foreach (var field in currentType.GetFields(
                         BindingFlags.Instance |
                         BindingFlags.Public |
                         BindingFlags.NonPublic |
                         BindingFlags.DeclaredOnly))
            {
                if (IsImmutableOrOpaque(field.FieldType)) continue;

                var fieldValue = field.GetValue(value);
                if (fieldValue is null) continue;

                var clonedFieldValue = DeepCloneInternal(fieldValue, visited);
                field.SetValue(clone, clonedFieldValue);
            }
            currentType = currentType.BaseType;
        }

        return clone;
    }

    private static bool IsImmutableOrOpaque(Type type)
    {
        var effectiveType = Nullable.GetUnderlyingType(type) ?? type;

        return effectiveType.IsPrimitive
               || effectiveType.IsEnum
               || effectiveType == typeof(string)
               || effectiveType == typeof(decimal)
               || effectiveType == typeof(DateTime)
               || effectiveType == typeof(DateTimeOffset)
               || effectiveType == typeof(TimeSpan)
               || effectiveType == typeof(Guid)
               || effectiveType == typeof(Uri)
               || effectiveType == typeof(IntPtr)
               || effectiveType == typeof(UIntPtr)
               || typeof(Type).IsAssignableFrom(effectiveType)
               || typeof(MemberInfo).IsAssignableFrom(effectiveType)
               || typeof(Assembly).IsAssignableFrom(effectiveType)
               || typeof(Delegate).IsAssignableFrom(effectiveType);
    }
}
