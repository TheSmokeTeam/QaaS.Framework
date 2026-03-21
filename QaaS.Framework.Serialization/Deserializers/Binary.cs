using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace QaaS.Framework.Serialization.Deserializers;

/// <summary>
/// Deserializes a byte[] to the requested C# object type.
/// </summary>
public class Binary: IDeserializer
{
    /// <inheritdoc />
    public object? Deserialize(byte[]? data, Type? deserializeType = null)
    {
        if (data is null) return null;

        if (deserializeType is null)
            throw new ArgumentException("Binary deserialization requires an explicit target type.",
                nameof(deserializeType));

        using var stream = new MemoryStream(data);
        var formatter = new BinaryFormatter
        {
            Binder = new AllowedTypesSerializationBinder(deserializeType)
        };

        var deserialized = formatter.Deserialize(stream);
        if (!deserializeType.IsInstanceOfType(deserialized))
        {
            throw new SerializationException(
                $"Binary payload deserialized to type '{deserialized.GetType().FullName}', " +
                $"which is not assignable to '{deserializeType.FullName}'.");
        }

        return deserialized;
    }

    private sealed class AllowedTypesSerializationBinder : SerializationBinder
    {
        private readonly HashSet<Type> _allowedTypes;

        public AllowedTypesSerializationBinder(Type rootType)
        {
            _allowedTypes = BuildAllowedTypes(rootType);
        }

        public override Type BindToType(string assemblyName, string typeName)
        {
            var resolvedType = ResolveType(assemblyName, typeName);
            if (IsTypeAllowed(resolvedType))
                return resolvedType;

            throw new SerializationException(
                $"Binary deserialization rejected type '{resolvedType.FullName}'.");
        }

        private bool IsTypeAllowed(Type resolvedType)
        {
            if (IsIntrinsicType(resolvedType))
                return true;

            if (_allowedTypes.Contains(resolvedType))
                return true;

            var normalizedType = NormalizeType(resolvedType);
            if (_allowedTypes.Contains(normalizedType))
                return true;

            return _allowedTypes.Any(allowedType =>
                allowedType != typeof(object) &&
                allowedType.IsAssignableFrom(resolvedType));
        }

        private static HashSet<Type> BuildAllowedTypes(Type rootType)
        {
            var allowedTypes = new HashSet<Type>();
            var queue = new Queue<Type>();

            AddType(rootType);

            while (queue.Count > 0)
            {
                var currentType = queue.Dequeue();

                foreach (var interfaceType in currentType.GetInterfaces())
                {
                    AddType(interfaceType);
                }

                if (currentType.IsArray)
                {
                    AddType(currentType.GetElementType());
                }

                if (currentType.IsGenericType)
                {
                    foreach (var genericArgument in currentType.GetGenericArguments())
                    {
                        AddType(genericArgument);
                    }
                }

                foreach (var field in currentType.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                            BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    AddType(field.FieldType);
                }

                foreach (var property in currentType.GetProperties(BindingFlags.Instance | BindingFlags.Public |
                                                                   BindingFlags.NonPublic |
                                                                   BindingFlags.DeclaredOnly))
                {
                    if (property.GetIndexParameters().Length == 0)
                    {
                        AddType(property.PropertyType);
                    }
                }
            }

            return allowedTypes;

            void AddType(Type? type)
            {
                if (type is null)
                    return;

                var normalizedType = NormalizeType(type);
                if (!allowedTypes.Add(normalizedType))
                    return;

                if (normalizedType != typeof(object))
                {
                    queue.Enqueue(normalizedType);
                }
            }
        }

        private static bool IsIntrinsicType(Type type)
        {
            var normalizedType = NormalizeType(type);
            return normalizedType.IsPrimitive ||
                   normalizedType.IsEnum ||
                   normalizedType == typeof(string) ||
                   normalizedType == typeof(decimal) ||
                   normalizedType == typeof(DateTime) ||
                   normalizedType == typeof(DateTimeOffset) ||
                   normalizedType == typeof(TimeSpan) ||
                   normalizedType == typeof(Guid) ||
                   normalizedType == typeof(Uri);
        }

        private static Type NormalizeType(Type type) => Nullable.GetUnderlyingType(type) ?? type;

        private static Type ResolveType(string assemblyName, string typeName)
        {
            var assemblyQualifiedName = $"{typeName}, {assemblyName}";
            var resolvedType = Type.GetType(assemblyQualifiedName, throwOnError: false);
            if (resolvedType is not null)
                return resolvedType;

            var requestedAssemblyName = new AssemblyName(assemblyName);
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(candidate =>
                string.Equals(candidate.FullName, assemblyName, StringComparison.Ordinal) ||
                string.Equals(candidate.GetName().Name, requestedAssemblyName.Name, StringComparison.Ordinal));

            assembly ??= Assembly.Load(requestedAssemblyName);
            return assembly.GetType(typeName, throwOnError: true)!;
        }
    }
}
