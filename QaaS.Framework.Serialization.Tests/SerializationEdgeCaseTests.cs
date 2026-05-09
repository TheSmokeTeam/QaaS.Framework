using System.Reflection;
using BinaryDeserializer = QaaS.Framework.Serialization.Deserializers.Binary;
using BinarySerializer = QaaS.Framework.Serialization.Serializers.Binary;
using MessagePackDeserializer = QaaS.Framework.Serialization.Deserializers.MessagePack;
using ProtobufDeserializer = QaaS.Framework.Serialization.Deserializers.ProtobufMessage;
using XmlDeserializer = QaaS.Framework.Serialization.Deserializers.Xml;
using XmlElementDeserializer = QaaS.Framework.Serialization.Deserializers.XmlElement;
using YamlDeserializer = QaaS.Framework.Serialization.Deserializers.Yaml;

namespace QaaS.Framework.Serialization.Tests;

[TestFixture]
public class SerializationEdgeCaseTests
{
    [Serializable]
    private sealed class LegacyBinaryPayload
    {
        public string Name { get; init; } = string.Empty;
        public int Count { get; init; }
    }

    [Test]
    public void BinaryDeserializer_WithSpecificType_ThrowsNotSupportedException()
    {
        // BinaryFormatter is disabled for security; any non-null input must throw.
        var deserializer = new BinaryDeserializer();
        Assert.Throws<NotSupportedException>(() =>
            deserializer.Deserialize(new byte[] { 1, 2, 3 }, typeof(string))
        );
    }

    [Test]
    public void BinaryDeserializer_WithoutSpecificType_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() =>
            new BinaryDeserializer().Deserialize(new byte[] { 1 })
        );
    }

    [Test]
    public void BinarySerializer_NonNullData_ThrowsNotSupportedException()
    {
        // BinaryFormatter is disabled for security; serializing any non-null object must throw.
        Assert.Throws<NotSupportedException>(() =>
            new BinarySerializer().Serialize(new LegacyBinaryPayload { Name = "alpha", Count = 2 })
        );
    }

    [Test]
    public void BinaryDeserializer_WithTypeHint_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() =>
            new BinaryDeserializer().Deserialize(new byte[] { 1 }, typeof(int))
        );
    }

    [Test]
    public void SerializerAndDeserializerFactories_InvalidEnum_ThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SerializerFactory.BuildSerializer((SerializationType)999)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DeserializerFactory.BuildDeserializer((SerializationType)999)
        );
    }

    [Test]
    public void SpecificTypeConfig_UsesEntryAssembly_WhenAssemblyNameIsMissing()
    {
        var entryAssemblyTypeName = Assembly
            .GetEntryAssembly()!
            .GetTypes()
            .First(type => !string.IsNullOrWhiteSpace(type.FullName))
            .FullName;
        var config = new SpecificTypeConfig { TypeFullName = entryAssemblyTypeName };

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
            TypeFullName = "QaaS.Framework.Serialization.Tests.DoesNotExist",
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
