using BinaryDeserializer = QaaS.Framework.Serialization.Deserializers.Binary;
using BinarySerializer = QaaS.Framework.Serialization.Serializers.Binary;
using MessagePackDeserializer = QaaS.Framework.Serialization.Deserializers.MessagePack;
using ProtobufDeserializer = QaaS.Framework.Serialization.Deserializers.ProtobufMessage;
using System.Runtime.Serialization;
using XmlDeserializer = QaaS.Framework.Serialization.Deserializers.Xml;
using XmlElementDeserializer = QaaS.Framework.Serialization.Deserializers.XmlElement;
using YamlDeserializer = QaaS.Framework.Serialization.Deserializers.Yaml;
using System.Reflection;

namespace QaaS.Framework.Serialization.Tests;

[TestFixture]
public class SerializationEdgeCaseTests
{
    [Test]
    public void BinaryDeserializer_WithSpecificType_ReturnsTypedPayload()
    {
        var serializer = new BinarySerializer();
        var deserializer = new BinaryDeserializer();
        const string payload = "typed";

        var bytes = serializer.Serialize(payload);
        var result = deserializer.Deserialize(bytes, typeof(string)) as string;

        Assert.That(result, Is.EqualTo(payload));
    }

    [Test]
    public void BinaryDeserializer_WithoutSpecificType_ReturnsRuntimePayload()
    {
        var bytes = new BinarySerializer().Serialize("typed");

        var result = new BinaryDeserializer().Deserialize(bytes);

        Assert.That(result, Is.EqualTo("typed"));
    }

    [Test]
    public void BinaryDeserializer_WithUnexpectedRuntimeType_ThrowsSerializationException()
    {
        var bytes = new BinarySerializer().Serialize("typed");

        Assert.Throws<SerializationException>(() =>
            new BinaryDeserializer().Deserialize(bytes, typeof(int)));
    }

    [Test]
    public void BinaryDeserializer_WithInterfaceTargetType_AllowsCompatibleGenericPayload()
    {
        var payload = new List<string> { "alpha", "beta" };
        var bytes = new BinarySerializer().Serialize(payload);

        var result = new BinaryDeserializer().Deserialize(bytes, typeof(IEnumerable<string>));

        Assert.That(result, Is.AssignableTo<IEnumerable<string>>());
        Assert.That(((IEnumerable<string>)result!).ToArray(), Is.EqualTo(payload));
    }

    [Test]
    public void SerializerAndDeserializerFactories_InvalidEnum_ThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SerializerFactory.BuildSerializer((SerializationType)999));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DeserializerFactory.BuildDeserializer((SerializationType)999));
    }

    [Test]
    public void SpecificTypeConfig_UsesEntryAssembly_WhenAssemblyNameIsMissing()
    {
        var entryAssemblyTypeName = Assembly.GetEntryAssembly()!.GetTypes()
            .First(type => !string.IsNullOrWhiteSpace(type.FullName))
            .FullName;
        var config = new SpecificTypeConfig
        {
            TypeFullName = entryAssemblyTypeName
        };

        var configuredType = config.GetConfiguredType();

        Assert.That(configuredType.FullName, Is.EqualTo(entryAssemblyTypeName));
        Assert.That(config.AssemblyName, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void SpecificTypeConfig_InvalidType_ThrowsTypeLoadException()
    {
        var config = new SpecificTypeConfig
        {
            AssemblyName = typeof(SerializationEdgeCaseTests).Assembly.FullName,
            TypeFullName = "QaaS.Framework.Serialization.Tests.DoesNotExist"
        };

        Assert.Throws<TypeLoadException>(() => config.GetConfiguredType());
    }

    [Test]
    public void Deserializers_ReturnNull_WhenInputBytesAreNull()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new MessagePackDeserializer().Deserialize(null, typeof(string)), Is.Null);
            Assert.That(new XmlDeserializer().Deserialize(null), Is.Null);
            Assert.That(new XmlElementDeserializer().Deserialize(null), Is.Null);
            Assert.That(new YamlDeserializer().Deserialize(null, typeof(string)), Is.Null);
            Assert.That(new ProtobufDeserializer().Deserialize(null, typeof(string)), Is.Null);
        });
    }
}
