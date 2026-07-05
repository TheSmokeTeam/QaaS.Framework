using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using MessagePack;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Session.CommunicationDataObjects;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.SDK.Tests;

/// <summary>
/// Covers the representation-aware cast fallback: bodies that arrive as deserialized
/// representations (JsonNode, yaml dictionaries, XDocument/XElement, raw bytes) are converted
/// to the requested type by the cast family instead of failing with an InvalidCastException.
/// This is the publish-consume scenario: a consumer without a configured specific type receives
/// representation bodies and still casts them to the producer's POCO in one call
/// </summary>
[TestFixture]
public class RepresentationCastFallbackTests
{
    public sealed class PersonPayload
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    [MessagePackObject]
    public sealed class MsgPackPersonPayload
    {
        [Key(0)]
        public string Name { get; set; } = string.Empty;

        [Key(1)]
        public int Age { get; set; }
    }

    private static JsonNode PersonNode(string name, int age) =>
        JsonNode.Parse($"{{\"Name\":\"{name}\",\"Age\":{age}}}")!;

    #region Data / DetailedData casts

    [Test]
    public void CastObjectData_ConvertsJsonNodeBody_ToRequestedType()
    {
        var data = new Data<object> { Body = PersonNode("ada", 36) };

        var casted = data.CastObjectData<PersonPayload>();

        Assert.Multiple(() =>
        {
            Assert.That(casted.Body!.Name, Is.EqualTo("ada"));
            Assert.That(casted.Body.Age, Is.EqualTo(36));
        });
    }

    [Test]
    public void CastObjectDetailedData_ConvertsJsonNodeBody_AndPreservesMetaDataAndTimestamp()
    {
        var timestamp = DateTime.UtcNow;
        var metaData = new MetaData { IoMatchIndex = 7 };
        var detailedData = new DetailedData<object>
        {
            Body = PersonNode("grace", 45),
            MetaData = metaData,
            Timestamp = timestamp,
        };

        var casted = detailedData.CastObjectDetailedData<PersonPayload>();

        Assert.Multiple(() =>
        {
            Assert.That(casted.Body!.Name, Is.EqualTo("grace"));
            Assert.That(casted.MetaData, Is.SameAs(metaData));
            Assert.That(casted.Timestamp, Is.EqualTo(timestamp));
        });
    }

    [Test]
    public void TryCastObjectData_AndTryCastObjectDetailedData_ReportConvertedRepresentations()
    {
        var data = new Data<object> { Body = PersonNode("linus", 30) };
        var detailedData = new DetailedData<object> { Body = PersonNode("linus", 31) };

        Assert.Multiple(() =>
        {
            Assert.That(data.TryCastObjectData<PersonPayload>(out var castedData), Is.True);
            Assert.That(castedData!.Body!.Age, Is.EqualTo(30));
            Assert.That(
                detailedData.TryCastObjectDetailedData<PersonPayload>(out var castedDetailed),
                Is.True
            );
            Assert.That(castedDetailed!.Body!.Age, Is.EqualTo(31));
        });
    }

    [Test]
    public void CastObjectData_StillThrowsIndicatively_WhenBodyIsNotConvertible()
    {
        var data = new Data<object> { Body = new object() };

        var exception = Assert.Throws<InvalidCastException>(() =>
            data.CastObjectData<PersonPayload>()
        );

        Assert.That(exception!.Message, Does.Contain(nameof(PersonPayload)));
    }

    [Test]
    public void CastObjectData_DoesNotConvertByteArrayBody_WithoutDeclaredSerializationType()
    {
        // Raw bytes keep their pass-through semantics: without a declared serialization type the
        // format cannot be inferred, so protocols that require byte[] bodies stay strict
        var bytes = QaasSerializer.Serialize(
            new PersonPayload { Name = "ada", Age = 36 },
            SerializationType.Json
        );
        var data = new Data<object> { Body = bytes };

        Assert.Throws<InvalidCastException>(() => data.CastObjectData<PersonPayload>());
    }

    [Test]
    public void CastObjectData_NullBody_ReturnsDefaultBody_ForValueAndReferenceTargets()
    {
        var data = new Data<object> { Body = null };

        Assert.Multiple(() =>
        {
            Assert.That(data.CastObjectData<int>().Body, Is.EqualTo(0));
            Assert.That(data.CastObjectData<PersonPayload>().Body, Is.Null);
        });
    }

    [Test]
    public void CastObjectDetailedData_NullBody_ReturnsDefaultBody_AndPreservesMetaDataAndTimestamp()
    {
        var timestamp = DateTime.UtcNow;
        var metaData = new MetaData { IoMatchIndex = 3 };
        var detailedData = new DetailedData<object>
        {
            Body = null,
            MetaData = metaData,
            Timestamp = timestamp,
        };

        var casted = detailedData.CastObjectDetailedData<int>();

        Assert.Multiple(() =>
        {
            Assert.That(casted.Body, Is.EqualTo(0));
            Assert.That(casted.MetaData, Is.SameAs(metaData));
            Assert.That(casted.Timestamp, Is.EqualTo(timestamp));
        });
    }

    [Test]
    public void TryCastObjectData_NullBody_SucceedsForValueTypeTargets()
    {
        var data = new Data<object> { Body = null };

        Assert.Multiple(() =>
        {
            Assert.That(data.TryCastObjectData<int>(out var casted), Is.True);
            Assert.That(casted!.Body, Is.EqualTo(0));
        });
    }

    #endregion

    #region GetBodyAs / TryGetBodyAs representations

    [Test]
    public void GetBodyAs_ConvertsYamlDictionaryBody_ToRequestedType()
    {
        var data = new Data<object>
        {
            Body = new Dictionary<object, object> { ["Name"] = "ada", ["Age"] = "36" },
        };

        var body = data.GetBodyAs<PersonPayload>();

        Assert.Multiple(() =>
        {
            Assert.That(body!.Name, Is.EqualTo("ada"));
            Assert.That(body.Age, Is.EqualTo(36));
        });
    }

    [Test]
    public void GetBodyAs_ConvertsXDocumentBody_ToRequestedType()
    {
        var data = new Data<object>
        {
            Body = XDocument.Parse("<PersonPayload><Name>ada</Name><Age>36</Age></PersonPayload>"),
        };

        var body = data.GetBodyAs<PersonPayload>();

        Assert.Multiple(() =>
        {
            Assert.That(body!.Name, Is.EqualTo("ada"));
            Assert.That(body.Age, Is.EqualTo(36));
        });
    }

    [Test]
    public void GetBodyAs_ConvertsXElementBody_ToRequestedType()
    {
        var data = new Data<object>
        {
            Body = XElement.Parse("<PersonPayload><Name>grace</Name><Age>45</Age></PersonPayload>"),
        };

        var body = data.GetBodyAs<PersonPayload>();

        Assert.That(body!.Name, Is.EqualTo("grace"));
    }

    [Test]
    public void TryGetBodyAs_ReportsConvertibleAndNonConvertibleRepresentations()
    {
        var convertible = new Data<object> { Body = PersonNode("ada", 36) };
        var nonConvertible = new Data<object> { Body = DateTime.UtcNow };

        Assert.Multiple(() =>
        {
            Assert.That(convertible.TryGetBodyAs<PersonPayload>(out var person), Is.True);
            Assert.That(person!.Name, Is.EqualTo("ada"));
            Assert.That(nonConvertible.TryGetBodyAs<PersonPayload>(out _), Is.False);
        });
    }

    #endregion

    #region CommunicationData with declared SerializationType

    [Test]
    public void CastCommunicationData_ConvertsJsonNodeBodies_UsingDeclaredSerializationType()
    {
        var communicationData = new CommunicationData<object>
        {
            Name = "people",
            SerializationType = SerializationType.Json,
            Data =
            [
                new DetailedData<object> { Body = PersonNode("ada", 36) },
                new DetailedData<object> { Body = PersonNode("grace", 45) },
            ],
        };

        var casted = communicationData.CastCommunicationData<PersonPayload>();

        Assert.Multiple(() =>
        {
            Assert.That(casted.Name, Is.EqualTo("people"));
            Assert.That(casted.SerializationType, Is.EqualTo(SerializationType.Json));
            Assert.That(
                casted.Data.Select(item => item.Body!.Name),
                Is.EqualTo(new[] { "ada", "grace" })
            );
        });
    }

    [Test]
    public void CastCommunicationData_ConvertsByteArrayBodies_WhenSerializationTypeIsDeclared()
    {
        // With a declared serialization type the raw-bytes format is known, so byte[] bodies convert
        var communicationData = new CommunicationData<object>
        {
            Name = "raw-people",
            SerializationType = SerializationType.Json,
            Data =
            [
                new DetailedData<object>
                {
                    Body = Encoding.UTF8.GetBytes("{\"Name\":\"ada\",\"Age\":36}"),
                },
            ],
        };

        var casted = communicationData.CastCommunicationData<PersonPayload>();

        Assert.That(casted.Data[0].Body!.Age, Is.EqualTo(36));
    }

    [Test]
    public void CastCommunicationData_WithByteArrayBodies_StillThrowsWithoutSerializationType()
    {
        var communicationData = new CommunicationData<object>
        {
            Name = "raw-unknown",
            SerializationType = null,
            Data =
            [
                new DetailedData<object>
                {
                    Body = Encoding.UTF8.GetBytes("{\"Name\":\"ada\",\"Age\":36}"),
                },
            ],
        };

        Assert.Throws<InvalidCastException>(() =>
            communicationData.CastCommunicationData<PersonPayload>()
        );
    }

    [Test]
    public void CastCommunicationData_ConvertsUntypedMessagePackBodies_UsingDeclaredSerializationType()
    {
        // An untyped messagepack deserialization yields object[] for keyed contracts; the declared
        // serialization type lets the cast round-trip it into the attributed POCO
        var bytes = MessagePackSerializer.Serialize(
            new MsgPackPersonPayload { Name = "ada", Age = 36 }
        );
        var untypedBody = MessagePackSerializer.Deserialize<object>(bytes);
        var communicationData = new CommunicationData<object>
        {
            Name = "msgpack-people",
            SerializationType = SerializationType.MessagePack,
            Data = [new DetailedData<object> { Body = untypedBody }],
        };

        var casted = communicationData.CastCommunicationData<MsgPackPersonPayload>();

        Assert.Multiple(() =>
        {
            Assert.That(casted.Data[0].Body!.Name, Is.EqualTo("ada"));
            Assert.That(casted.Data[0].Body!.Age, Is.EqualTo(36));
        });
    }

    [Test]
    public void GetBodiesAs_ConvertsRepresentationBodies_UsingDeclaredSerializationType()
    {
        var communicationData = new CommunicationData<object>
        {
            Name = "mixed",
            SerializationType = SerializationType.Json,
            Data =
            [
                new DetailedData<object>
                {
                    Body = new PersonPayload { Name = "typed", Age = 1 },
                },
                new DetailedData<object> { Body = PersonNode("converted", 2) },
            ],
        };

        var bodies = communicationData.GetBodiesAs<PersonPayload>();

        Assert.That(bodies.Select(body => body!.Name), Is.EqualTo(new[] { "typed", "converted" }));
    }

    [Test]
    public void TryCastCommunicationData_ReportsConvertedRepresentationBodies()
    {
        var communicationData = new CommunicationData<object>
        {
            Name = "try-people",
            SerializationType = SerializationType.Json,
            Data = [new DetailedData<object> { Body = PersonNode("ada", 36) }],
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                communicationData.TryCastCommunicationData<PersonPayload>(out var casted),
                Is.True
            );
            Assert.That(casted!.Data[0].Body!.Name, Is.EqualTo("ada"));
        });
    }

    [Test]
    public void CastCommunicationData_ValueTypeBodies_ConvertFromJsonNodes()
    {
        var communicationData = new CommunicationData<object>
        {
            Name = "numbers",
            SerializationType = SerializationType.Json,
            Data = [new DetailedData<object> { Body = JsonNode.Parse("42") }],
        };

        var casted = communicationData.CastCommunicationData<int>();

        Assert.That(casted.Data[0].Body, Is.EqualTo(42));
    }

    [Test]
    public void CastCommunicationData_NullBodies_CastToValueTypeDefault()
    {
        var communicationData = new CommunicationData<object>
        {
            Name = "numbers",
            SerializationType = SerializationType.Json,
            Data = [new DetailedData<object> { Body = null }],
        };

        var casted = communicationData.CastCommunicationData<int>();

        Assert.That(casted.Data[0].Body, Is.EqualTo(0));
    }

    #endregion
}
