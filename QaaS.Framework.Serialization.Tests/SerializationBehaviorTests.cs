using System.Xml.Linq;
using Google.Protobuf.WellKnownTypes;
using QaaS.Framework.Serialization.Deserializers;
using QaaS.Framework.Serialization.Serializers;

namespace QaaS.Framework.Serialization.Tests;

[TestFixture]
public class SerializationBehaviorTests
{
    private sealed class TestPayload
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    [Test]
    public void SerializerFactory_ReturnsExpectedSerializerForEachType()
    {
        Assert.That(SerializerFactory.BuildSerializer(SerializationType.Binary), Is.TypeOf<Serializers.Binary>());
        Assert.That(SerializerFactory.BuildSerializer(SerializationType.Json), Is.TypeOf<Serializers.Json>());
        Assert.That(SerializerFactory.BuildSerializer(SerializationType.MessagePack), Is.TypeOf<Serializers.MessagePack>());
        Assert.That(SerializerFactory.BuildSerializer(SerializationType.Xml), Is.TypeOf<Serializers.Xml>());
        Assert.That(SerializerFactory.BuildSerializer(SerializationType.Yaml), Is.TypeOf<Serializers.Yaml>());
        Assert.That(SerializerFactory.BuildSerializer(SerializationType.ProtobufMessage), Is.TypeOf<Serializers.ProtobufMessage>());
        Assert.That(SerializerFactory.BuildSerializer(SerializationType.XmlElement), Is.TypeOf<Serializers.XmlElement>());
        Assert.That(SerializerFactory.BuildSerializer(null), Is.Null);
    }

    [Test]
    public void DeserializerFactory_ReturnsExpectedDeserializerForEachType()
    {
        Assert.That(DeserializerFactory.BuildDeserializer(SerializationType.Binary), Is.TypeOf<Deserializers.Binary>());
        Assert.That(DeserializerFactory.BuildDeserializer(SerializationType.Json), Is.TypeOf<Deserializers.Json>());
        Assert.That(DeserializerFactory.BuildDeserializer(SerializationType.MessagePack), Is.TypeOf<Deserializers.MessagePack>());
        Assert.That(DeserializerFactory.BuildDeserializer(SerializationType.Xml), Is.TypeOf<Deserializers.Xml>());
        Assert.That(DeserializerFactory.BuildDeserializer(SerializationType.Yaml), Is.TypeOf<Deserializers.Yaml>());
        Assert.That(DeserializerFactory.BuildDeserializer(SerializationType.ProtobufMessage), Is.TypeOf<Deserializers.ProtobufMessage>());
        Assert.That(DeserializerFactory.BuildDeserializer(SerializationType.XmlElement), Is.TypeOf<Deserializers.XmlElement>());
        Assert.That(DeserializerFactory.BuildDeserializer(null), Is.Null);
    }

    [Test]
    public void JsonSerializer_AndDeserializer_RoundTripPayload()
    {
        var serializer = new Serializers.Json();
        var deserializer = new Deserializers.Json();
        var payload = new TestPayload { Name = "alpha", Count = 7 };

        var bytes = serializer.Serialize(payload);
        var result = deserializer.Deserialize(bytes, typeof(TestPayload)) as TestPayload;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("alpha"));
        Assert.That(result.Count, Is.EqualTo(7));
    }

    [Test]
    public void MessagePackSerializer_AndDeserializer_RoundTripPayload()
    {
        var serializer = new Serializers.MessagePack();
        var deserializer = new Deserializers.MessagePack();
        const string payload = "beta";

        var bytes = serializer.Serialize(payload);
        var result = deserializer.Deserialize(bytes, typeof(string)) as string;

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo("beta"));
    }

    [Test]
    public void BinarySerializer_AndDeserializer_RoundTripPayload()
    {
        var serializer = new Serializers.Binary();
        var deserializer = new Deserializers.Binary();
        const string payload = "gamma";

        var bytes = serializer.Serialize(payload);
        var result = deserializer.Deserialize(bytes);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo("gamma"));
    }

    [Test]
    public void XmlSerializer_AndDeserializer_RoundTripXDocument()
    {
        var serializer = new Serializers.Xml();
        var deserializer = new Deserializers.Xml();
        var payload = XDocument.Parse("<root><value>42</value></root>");

        var bytes = serializer.Serialize(payload);
        var result = deserializer.Deserialize(bytes) as XDocument;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Root!.Element("value")!.Value, Is.EqualTo("42"));
    }

    [Test]
    public void XmlElementSerializer_AndDeserializer_RoundTripXElement()
    {
        var serializer = new Serializers.XmlElement();
        var deserializer = new Deserializers.XmlElement();
        var payload = XElement.Parse("<node><id>123</id></node>");

        var bytes = serializer.Serialize(payload);
        var result = deserializer.Deserialize(bytes) as XElement;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Element("id")!.Value, Is.EqualTo("123"));
    }

    [Test]
    public void YamlSerializer_AndDeserializer_RoundTripPayload()
    {
        var serializer = new Serializers.Yaml();
        var deserializer = new Deserializers.Yaml();
        var payload = new TestPayload { Name = "delta", Count = 5 };

        var bytes = serializer.Serialize(payload);
        var result = deserializer.Deserialize(bytes, typeof(TestPayload)) as TestPayload;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("delta"));
        Assert.That(result.Count, Is.EqualTo(5));
    }

    [Test]
    public void ProtobufSerializer_AndDeserializer_RoundTripStringValue()
    {
        var serializer = new Serializers.ProtobufMessage();
        var deserializer = new Deserializers.ProtobufMessage();
        var payload = new StringValue { Value = "protobuf" };

        var bytes = serializer.Serialize(payload);
        var result = deserializer.Deserialize(bytes, typeof(StringValue)) as StringValue;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value, Is.EqualTo("protobuf"));
    }

    [Test]
    public void ProtobufDeserializer_WithoutType_Throws()
    {
        var deserializer = new Deserializers.ProtobufMessage();
        var bytes = new Serializers.ProtobufMessage().Serialize(new StringValue { Value = "x" })!;

        Assert.Throws<ArgumentException>(() => deserializer.Deserialize(bytes, null));
    }

    [Test]
    public void SpecificTypeConfig_GetConfiguredType_ReturnsExpectedType()
    {
        var config = new SpecificTypeConfig
        {
            AssemblyName = typeof(TestPayload).Assembly.FullName,
            TypeFullName = typeof(TestPayload).FullName
        };

        var configuredType = config.GetConfiguredType();

        Assert.That(configuredType, Is.EqualTo(typeof(TestPayload)));
    }

    [Test]
    public void BinaryAndJsonSerializers_ReturnNull_WhenDataIsNull()
    {
        Assert.That(new Serializers.Binary().Serialize(null), Is.Null);
        Assert.That(new Serializers.Json().Serialize(null), Is.Null);
        Assert.That(new Deserializers.Binary().Deserialize(null), Is.Null);
        Assert.That(new Deserializers.Json().Deserialize(null), Is.Null);
    }
}
