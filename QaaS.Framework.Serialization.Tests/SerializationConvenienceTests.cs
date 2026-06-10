using System.Text;
using System.Xml.Linq;
using Google.Protobuf.WellKnownTypes;
using QaaS.Framework.Serialization.Serializers;
using IDeserializer = QaaS.Framework.Serialization.Deserializers.IDeserializer;

namespace QaaS.Framework.Serialization.Tests;

[TestFixture]
public class SerializationConvenienceTests
{
    private sealed class ConveniencePayload
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    #region QaasSerializer facade

    [Test]
    public void QaasSerializer_Json_RoundTripsTypedPayloadInOneCallEach()
    {
        var payload = new ConveniencePayload { Name = "alpha", Count = 7 };

        var bytes = QaasSerializer.Serialize(payload, SerializationType.Json);
        var result = QaasSerializer.Deserialize<ConveniencePayload>(bytes, SerializationType.Json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("alpha"));
        Assert.That(result.Count, Is.EqualTo(7));
    }

    [Test]
    public void QaasSerializer_Yaml_RoundTripsTypedPayloadInOneCallEach()
    {
        var payload = new ConveniencePayload { Name = "beta", Count = 3 };

        var bytes = QaasSerializer.Serialize(payload, SerializationType.Yaml);
        var result = QaasSerializer.Deserialize<ConveniencePayload>(bytes, SerializationType.Yaml);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("beta"));
        Assert.That(result.Count, Is.EqualTo(3));
    }

    [Test]
    public void QaasSerializer_MessagePack_RoundTripsString()
    {
        var bytes = QaasSerializer.Serialize("gamma", SerializationType.MessagePack);
        var result = QaasSerializer.Deserialize<string>(bytes, SerializationType.MessagePack);

        Assert.That(result, Is.EqualTo("gamma"));
    }

    [Test]
    public void QaasSerializer_Binary_RoundTripsString()
    {
        var bytes = QaasSerializer.Serialize("delta", SerializationType.Binary);
        var result = QaasSerializer.Deserialize<string>(bytes, SerializationType.Binary);

        Assert.That(result, Is.EqualTo("delta"));
    }

    [Test]
    public void QaasSerializer_Xml_RoundTripsXDocument()
    {
        var payload = XDocument.Parse("<root><value>42</value></root>");

        var bytes = QaasSerializer.Serialize(payload, SerializationType.Xml);
        var result = QaasSerializer.Deserialize<XDocument>(bytes, SerializationType.Xml);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Root!.Element("value")!.Value, Is.EqualTo("42"));
    }

    [Test]
    public void QaasSerializer_XmlElement_RoundTripsXElement()
    {
        var payload = XElement.Parse("<node><id>123</id></node>");

        var bytes = QaasSerializer.Serialize(payload, SerializationType.XmlElement);
        var result = QaasSerializer.Deserialize<XElement>(bytes, SerializationType.XmlElement);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Element("id")!.Value, Is.EqualTo("123"));
    }

    [Test]
    public void QaasSerializer_ProtobufMessage_RoundTripsStringValue()
    {
        var payload = new StringValue { Value = "protobuf" };

        var bytes = QaasSerializer.Serialize(payload, SerializationType.ProtobufMessage);
        var result = QaasSerializer.Deserialize<StringValue>(bytes, SerializationType.ProtobufMessage);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value, Is.EqualTo("protobuf"));
    }

    [Test]
    public void QaasSerializer_SerializeToString_AndDeserializeFromString_RoundTripJson()
    {
        var payload = new ConveniencePayload { Name = "text", Count = 11 };

        var json = QaasSerializer.SerializeToString(payload, SerializationType.Json);
        var result = QaasSerializer.DeserializeFromString<ConveniencePayload>(json, SerializationType.Json);

        Assert.That(json, Does.Contain("\"text\""));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("text"));
        Assert.That(result.Count, Is.EqualTo(11));
    }

    [Test]
    public void QaasSerializer_DeserializeFromString_WithoutTargetType_ReturnsDefaultRepresentation()
    {
        var result = QaasSerializer.DeserializeFromString("{\"a\": 1}", SerializationType.Json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ToString(), Does.Contain("1"));
    }

    [Test]
    public void QaasSerializer_NullData_ReturnsNullOrDefault()
    {
        Assert.Multiple(() =>
        {
            Assert.That(QaasSerializer.Serialize(null, SerializationType.Json), Is.Null);
            Assert.That(QaasSerializer.SerializeToString(null, SerializationType.Json), Is.Null);
            Assert.That(QaasSerializer.Deserialize<ConveniencePayload>(null, SerializationType.Json), Is.Null);
            Assert.That(QaasSerializer.DeserializeFromString<ConveniencePayload>(null, SerializationType.Json),
                Is.Null);
        });
    }

    [Test]
    public void QaasSerializer_NullSerializationType_PassesByteArrayThrough()
    {
        var raw = "raw-bytes"u8.ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(QaasSerializer.Serialize(raw, null), Is.SameAs(raw));
            Assert.That(QaasSerializer.Serialize(null, null), Is.Null);
            Assert.That(QaasSerializer.Deserialize<byte[]>(raw, null), Is.SameAs(raw));
            Assert.That(QaasSerializer.Deserialize(raw, null), Is.SameAs(raw));
            Assert.That(QaasSerializer.Deserialize<byte[]>(null, null), Is.Null);
        });
    }

    [Test]
    public void QaasSerializer_NullSerializationType_WithNonByteData_ThrowsIndicativeException()
    {
        var serializeException = Assert.Throws<QaasSerializationException>(() =>
            QaasSerializer.Serialize(new ConveniencePayload(), null));
        var deserializeException = Assert.Throws<QaasSerializationException>(() =>
            QaasSerializer.Deserialize<ConveniencePayload>("raw"u8.ToArray(), null));

        Assert.That(serializeException!.Message, Does.Contain("No serialization type was given"));
        Assert.That(deserializeException!.Message, Does.Contain("No serialization type was given"));
    }

    [Test]
    public void QaasSerializer_SerializeFailure_ThrowsIndicativeExceptionWithInnerException()
    {
        // The Xml serializer requires an XDocument, anything else fails inside the serializer
        var exception = Assert.Throws<QaasSerializationException>(() =>
            QaasSerializer.Serialize(new ConveniencePayload(), SerializationType.Xml));

        Assert.That(exception!.Message, Does.Contain("Failed to serialize"));
        Assert.That(exception.Message, Does.Contain(nameof(ConveniencePayload)));
        Assert.That(exception.Message, Does.Contain("Xml"));
        Assert.That(exception.InnerException, Is.Not.Null);
    }

    [Test]
    public void QaasSerializer_DeserializeFailure_ThrowsIndicativeExceptionWithInnerException()
    {
        var notJson = "{definitely-not-json"u8.ToArray();

        var exception = Assert.Throws<QaasSerializationException>(() =>
            QaasSerializer.Deserialize<ConveniencePayload>(notJson, SerializationType.Json));

        Assert.That(exception!.Message, Does.Contain("Failed to deserialize"));
        Assert.That(exception.Message, Does.Contain("Json"));
        Assert.That(exception.InnerException, Is.Not.Null);
    }

    [Test]
    public void QaasSerializer_DeserializeToIncompatibleType_ThrowsIndicativeException()
    {
        // The Xml deserializer always produces XDocument, requesting a payload type cannot be satisfied
        var bytes = QaasSerializer.Serialize(XDocument.Parse("<a/>"), SerializationType.Xml);

        var exception = Assert.Throws<QaasSerializationException>(() =>
            QaasSerializer.Deserialize<ConveniencePayload>(bytes, SerializationType.Xml));

        Assert.That(exception!.Message, Does.Contain("not assignable"));
        Assert.That(exception.Message, Does.Contain(nameof(ConveniencePayload)));
    }

    [Test]
    public void QaasSerializer_TrySerialize_ReportsSuccessAndFailureWithoutThrowing()
    {
        var payload = new ConveniencePayload { Name = "try", Count = 1 };

        var success = QaasSerializer.TrySerialize(payload, SerializationType.Json, out var serialized);
        var failure = QaasSerializer.TrySerialize(payload, SerializationType.Xml, out var failed);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(serialized, Is.Not.Null);
            Assert.That(failure, Is.False);
            Assert.That(failed, Is.Null);
        });
    }

    [Test]
    public void QaasSerializer_TryDeserialize_ReportsSuccessAndFailureWithoutThrowing()
    {
        var valid = QaasSerializer.Serialize(new ConveniencePayload { Name = "ok", Count = 2 },
            SerializationType.Json);

        var success = QaasSerializer.TryDeserialize<ConveniencePayload>(valid, SerializationType.Json,
            out var deserialized);
        var failure = QaasSerializer.TryDeserialize<ConveniencePayload>("nope{"u8.ToArray(),
            SerializationType.Json, out var failed);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(deserialized!.Name, Is.EqualTo("ok"));
            Assert.That(failure, Is.False);
            Assert.That(failed, Is.Null);
        });
    }

    [Test]
    public void QaasSerializer_TryDeserializeFromString_ReportsSuccessAndFailureWithoutThrowing()
    {
        var success = QaasSerializer.TryDeserializeFromString<ConveniencePayload>(
            "{\"Name\":\"ok\",\"Count\":5}", SerializationType.Json, out var deserialized);
        var failure = QaasSerializer.TryDeserializeFromString<ConveniencePayload>(
            "{broken", SerializationType.Json, out var failed);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(deserialized!.Count, Is.EqualTo(5));
            Assert.That(failure, Is.False);
            Assert.That(failed, Is.Null);
        });
    }

    #endregion

    #region SerializationType extensions

    [Test]
    public void SerializationTypeExtensions_BuildSerializer_MatchesFactoryForEveryType()
    {
        foreach (var serializationType in System.Enum.GetValues<SerializationType>())
        {
            Assert.That(serializationType.BuildSerializer(),
                Is.TypeOf(SerializerFactory.BuildSerializer(serializationType)!.GetType()),
                $"Serializer mismatch for {serializationType}");
            Assert.That(serializationType.BuildDeserializer(),
                Is.TypeOf(DeserializerFactory.BuildDeserializer(serializationType)!.GetType()),
                $"Deserializer mismatch for {serializationType}");
        }
    }

    [Test]
    public void SerializationTypeExtensions_InvalidEnum_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((SerializationType)999).BuildSerializer());
        Assert.Throws<ArgumentOutOfRangeException>(() => ((SerializationType)999).BuildDeserializer());
    }

    [Test]
    public void SerializationTypeExtensions_EnableFluentRoundTrip()
    {
        var payload = new ConveniencePayload { Name = "fluent", Count = 9 };

        var bytes = SerializationType.Json.BuildSerializer().Serialize(payload);
        var result = SerializationType.Json.BuildDeserializer().Deserialize<ConveniencePayload>(bytes);

        Assert.That(result!.Name, Is.EqualTo("fluent"));
    }

    #endregion

    #region ISerializer / IDeserializer instance extensions

    [Test]
    public void SerializerExtensions_SerializeToString_ReturnsUtf8Text()
    {
        var json = new Json().SerializeToString(new ConveniencePayload { Name = "stringy", Count = 4 });

        Assert.That(json, Does.Contain("\"stringy\""));
        Assert.That(new Json().SerializeToString(null), Is.Null);
    }

    [Test]
    public void SerializerExtensions_TrySerialize_ReportsSuccessAndFailureWithoutThrowing()
    {
        var success = new Json().TrySerialize(new ConveniencePayload(), out var serialized);
        var failure = new Xml().TrySerialize(new ConveniencePayload(), out var failed);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(serialized, Is.Not.Null);
            Assert.That(failure, Is.False);
            Assert.That(failed, Is.Null);
        });
    }

    [Test]
    public void DeserializerExtensions_GenericDeserialize_ReturnsTypedResultWithoutManualCast()
    {
        var bytes = new Json().Serialize(new ConveniencePayload { Name = "typed", Count = 6 });

        ConveniencePayload? result = new Deserializers.Json().Deserialize<ConveniencePayload>(bytes);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("typed"));
        Assert.That(new Deserializers.Json().Deserialize<ConveniencePayload>(null), Is.Null);
    }

    [Test]
    public void DeserializerExtensions_GenericDeserialize_IncompatibleProducedType_ThrowsIndicativeException()
    {
        var bytes = new Xml().Serialize(XDocument.Parse("<a/>"));

        var exception = Assert.Throws<QaasSerializationException>(() =>
            new Deserializers.Xml().Deserialize<ConveniencePayload>(bytes));

        Assert.That(exception!.Message, Does.Contain("not assignable"));
    }

    [Test]
    public void DeserializerExtensions_DeserializeFromString_RoundTripsTextFormats()
    {
        var typed = new Deserializers.Json()
            .DeserializeFromString<ConveniencePayload>("{\"Name\":\"fromString\",\"Count\":8}");
        var untyped = new Deserializers.Json().DeserializeFromString("{\"a\": 1}");

        Assert.Multiple(() =>
        {
            Assert.That(typed!.Name, Is.EqualTo("fromString"));
            Assert.That(typed.Count, Is.EqualTo(8));
            Assert.That(untyped, Is.Not.Null);
            Assert.That(new Deserializers.Json().DeserializeFromString<ConveniencePayload>(null), Is.Null);
            Assert.That(new Deserializers.Json().DeserializeFromString(null), Is.Null);
        });
    }

    [Test]
    public void DeserializerExtensions_TryDeserialize_ReportsSuccessAndFailureWithoutThrowing()
    {
        IDeserializer deserializer = new Deserializers.Json();
        var valid = new Json().Serialize(new ConveniencePayload { Name = "ok", Count = 2 });

        var success = deserializer.TryDeserialize<ConveniencePayload>(valid, out var deserialized);
        var failure = deserializer.TryDeserialize<ConveniencePayload>("}{"u8.ToArray(), out var failed);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(deserialized!.Name, Is.EqualTo("ok"));
            Assert.That(failure, Is.False);
            Assert.That(failed, Is.Null);
        });
    }

    [Test]
    public void DeserializerExtensions_RoundTripThroughStrings_PreservesUnicodeContent()
    {
        const string unicodeName = "שלום-héllo-😀";
        var json = new Json().SerializeToString(new ConveniencePayload { Name = unicodeName, Count = 1 });

        var result = new Deserializers.Json().DeserializeFromString<ConveniencePayload>(json);

        Assert.That(result!.Name, Is.EqualTo(unicodeName));
        Assert.That(Encoding.UTF8.GetBytes(json!), Is.EqualTo(new Json().Serialize(
            new ConveniencePayload { Name = unicodeName, Count = 1 })));
    }

    #endregion
}
