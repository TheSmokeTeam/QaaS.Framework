using System.Text.Json.Nodes;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Session;
using QaaS.Framework.SDK.Session.CommunicationDataObjects;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.SDK.Tests;

[TestFixture]
public class CastingConvenienceTests
{
    private sealed class OrderPayload
    {
        public string Id { get; set; } = string.Empty;
        public int Amount { get; set; }
    }

    private static DetailedData<object> JsonNodeDetailedData(string id, int amount, int? ioMatchIndex = null) =>
        new()
        {
            Body = JsonNode.Parse($"{{\"Id\":\"{id}\",\"Amount\":{amount}}}"),
            MetaData = ioMatchIndex == null ? null : new MetaData { IoMatchIndex = ioMatchIndex },
            Timestamp = DateTime.UtcNow
        };

    #region Data casting helpers

    [Test]
    public void TryCastObjectData_ReturnsTrueAndCasted_WhenBodyMatches()
    {
        var data = new Data<object> { Body = "hello" };

        var success = data.TryCastObjectData<string>(out var casted);

        Assert.That(success, Is.True);
        Assert.That(casted!.Body, Is.EqualTo("hello"));
    }

    [Test]
    public void TryCastObjectData_ReturnsFalse_WhenBodyDoesNotMatch()
    {
        var data = new Data<object> { Body = "not-an-int" };

        var success = data.TryCastObjectData<int>(out var casted);

        Assert.That(success, Is.False);
        Assert.That(casted, Is.Null);
    }

    [Test]
    public void TryCastObjectDetailedData_ReturnsTrueAndPreservesFields_WhenBodyMatches()
    {
        var timestamp = DateTime.UtcNow;
        var metaData = new MetaData { IoMatchIndex = 4 };
        var detailedData = new DetailedData<object>
        {
            Body = "hello",
            MetaData = metaData,
            Timestamp = timestamp
        };

        var success = detailedData.TryCastObjectDetailedData<string>(out var casted);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(casted!.Body, Is.EqualTo("hello"));
            Assert.That(casted.MetaData, Is.SameAs(metaData));
            Assert.That(casted.Timestamp, Is.EqualTo(timestamp));
        });
    }

    [Test]
    public void TryCastObjectDetailedData_ReturnsFalse_WhenBodyDoesNotMatch()
    {
        var detailedData = new DetailedData<object> { Body = "not-an-int" };

        var success = detailedData.TryCastObjectDetailedData<int>(out var casted);

        Assert.That(success, Is.False);
        Assert.That(casted, Is.Null);
    }

    [Test]
    public void GetBodyAs_ReturnsTypedBodyDirectly()
    {
        var detailedData = new DetailedData<object> { Body = "direct" };

        Assert.That(detailedData.GetBodyAs<string>(), Is.EqualTo("direct"));
    }

    [Test]
    public void GetBodyAs_ReturnsDefault_WhenBodyIsNull()
    {
        var data = new Data<object> { Body = null };

        Assert.That(data.GetBodyAs<string>(), Is.Null);
    }

    [Test]
    public void GetBodyAs_ThrowsIndicativeException_WhenBodyDoesNotMatchAndCannotBeConverted()
    {
        var data = new Data<object> { Body = 42 };

        var exception = Assert.Throws<InvalidCastException>(() => data.GetBodyAs<OrderPayload>());

        Assert.That(exception!.Message, Does.Contain("Int32"));
        Assert.That(exception.Message, Does.Contain(nameof(OrderPayload)));
        Assert.That(exception.Message, Does.Contain("ConvertBodyTo"));
    }

    [Test]
    public void GetBodyAs_ConvertsDeserializedJsonNodeBody_ToRequestedType()
    {
        var data = new Data<object> { Body = JsonNode.Parse("{\"Id\":\"o-9\",\"Amount\":90}") };

        var body = data.GetBodyAs<OrderPayload>();

        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Id, Is.EqualTo("o-9"));
        Assert.That(body.Amount, Is.EqualTo(90));
    }

    [Test]
    public void TryGetBodyAs_CoversMatchingNullAndMismatchedBodies()
    {
        var matching = new Data<object> { Body = 42 };
        var nullBody = new Data<object> { Body = null };
        var mismatched = new Data<object> { Body = "string" };

        Assert.Multiple(() =>
        {
            Assert.That(matching.TryGetBodyAs<int>(out var matched), Is.True);
            Assert.That(matched, Is.EqualTo(42));
            Assert.That(nullBody.TryGetBodyAs<string>(out var defaulted), Is.True);
            Assert.That(defaulted, Is.Null);
            Assert.That(mismatched.TryGetBodyAs<int>(out _), Is.False);
        });
    }

    #endregion

    #region Data conversion helpers

    [Test]
    public void ConvertBodyTo_ReturnsBodyAsIs_WhenAlreadyRequestedType()
    {
        var payload = new OrderPayload { Id = "o-1", Amount = 5 };
        var data = new Data<object> { Body = payload };

        Assert.That(data.ConvertBodyTo<OrderPayload>(SerializationType.Json), Is.SameAs(payload));
    }

    [Test]
    public void ConvertBodyTo_DeserializesByteArrayBody()
    {
        var serialized = QaasSerializer.Serialize(new OrderPayload { Id = "o-2", Amount = 9 },
            SerializationType.Json);
        var data = new Data<object> { Body = serialized };

        var converted = data.ConvertBodyTo<OrderPayload>(SerializationType.Json);

        Assert.That(converted!.Id, Is.EqualTo("o-2"));
        Assert.That(converted.Amount, Is.EqualTo(9));
    }

    [Test]
    public void ConvertBodyTo_RoundTripsDeserializedRepresentationIntoRequestedType()
    {
        // This is the everyday case: session data bodies arrive as JsonNode, the user wants their POCO
        var data = new Data<object> { Body = JsonNode.Parse("{\"Id\":\"o-3\",\"Amount\":12}") };

        var converted = data.ConvertBodyTo<OrderPayload>(SerializationType.Json);

        Assert.That(converted!.Id, Is.EqualTo("o-3"));
        Assert.That(converted.Amount, Is.EqualTo(12));
    }

    [Test]
    public void ConvertBodyTo_ReturnsDefault_WhenBodyIsNull()
    {
        var data = new Data<object> { Body = null };

        Assert.That(data.ConvertBodyTo<OrderPayload>(SerializationType.Json), Is.Null);
    }

    [Test]
    public void ConvertData_ConvertsBodyAndPreservesMetaData()
    {
        var metaData = new MetaData { IoMatchIndex = 2 };
        var data = new Data<object>
        {
            Body = JsonNode.Parse("{\"Id\":\"o-4\",\"Amount\":1}"),
            MetaData = metaData
        };

        var converted = data.ConvertData<OrderPayload>(SerializationType.Json);

        Assert.Multiple(() =>
        {
            Assert.That(converted.Body!.Id, Is.EqualTo("o-4"));
            Assert.That(converted.MetaData, Is.SameAs(metaData));
        });
    }

    [Test]
    public void ConvertDetailedData_ConvertsBodyAndPreservesMetaDataAndTimestamp()
    {
        var timestamp = DateTime.UtcNow;
        var metaData = new MetaData { IoMatchIndex = 3 };
        var detailedData = new DetailedData<object>
        {
            Body = JsonNode.Parse("{\"Id\":\"o-5\",\"Amount\":2}"),
            MetaData = metaData,
            Timestamp = timestamp
        };

        var converted = detailedData.ConvertDetailedData<OrderPayload>(SerializationType.Json);

        Assert.Multiple(() =>
        {
            Assert.That(converted.Body!.Id, Is.EqualTo("o-5"));
            Assert.That(converted.MetaData, Is.SameAs(metaData));
            Assert.That(converted.Timestamp, Is.EqualTo(timestamp));
        });
    }

    [Test]
    public void ConvertBodyTo_FailingConversion_ThrowsQaasSerializationException()
    {
        var data = new Data<object> { Body = "definitely { not json"u8.ToArray() };

        Assert.Throws<QaasSerializationException>(() =>
            data.ConvertBodyTo<OrderPayload>(SerializationType.Json));
    }

    #endregion

    #region CommunicationData helpers

    [Test]
    public void TryGetCommunicationDataByName_CoversFoundMissingAndDuplicateNames()
    {
        var communicationDataList = new List<CommunicationData<object>>
        {
            new() { Name = "unique", Data = [] },
            new() { Name = "duplicated", Data = [] },
            new() { Name = "duplicated", Data = [] }
        };

        Assert.Multiple(() =>
        {
            Assert.That(communicationDataList.TryGetCommunicationDataByName("unique", out var found), Is.True);
            Assert.That(found!.Name, Is.EqualTo("unique"));
            Assert.That(communicationDataList.TryGetCommunicationDataByName("missing", out var missing), Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(communicationDataList.TryGetCommunicationDataByName("duplicated", out var duplicated),
                Is.False);
            Assert.That(duplicated, Is.Null);
            Assert.That(((List<CommunicationData<object>>?)null)
                .TryGetCommunicationDataByName("any", out _), Is.False);
        });
    }

    [Test]
    public void TryCastCommunicationData_ReportsSuccessAndFailureWithoutThrowing()
    {
        var castable = new CommunicationData<object>
        {
            Name = "castable",
            Data = [new DetailedData<object> { Body = "text" }]
        };
        var notCastable = new CommunicationData<object>
        {
            Name = "not-castable",
            Data = [new DetailedData<object> { Body = "text" }]
        };

        Assert.Multiple(() =>
        {
            Assert.That(castable.TryCastCommunicationData<string>(out var casted), Is.True);
            Assert.That(casted!.Data[0].Body, Is.EqualTo("text"));
            Assert.That(notCastable.TryCastCommunicationData<int>(out var failed), Is.False);
            Assert.That(failed, Is.Null);
        });
    }

    [Test]
    public void TryGetDataByIoMatchIndex_ReportsSuccessAndFailureWithoutThrowing()
    {
        var communicationData = new CommunicationData<object>
        {
            Name = "indexed",
            Data =
            [
                new DetailedData<object> { Body = "first", MetaData = new MetaData { IoMatchIndex = 1 } },
                new DetailedData<object> { Body = "second", MetaData = new MetaData { IoMatchIndex = 2 } }
            ]
        };

        Assert.Multiple(() =>
        {
            Assert.That(communicationData.TryGetDataByIoMatchIndex(2, out var found), Is.True);
            Assert.That(found!.Body, Is.EqualTo("second"));
            Assert.That(communicationData.TryGetDataByIoMatchIndex(99, out var missing), Is.False);
            Assert.That(missing, Is.Null);
        });
    }

    [Test]
    public void GetBodies_ReturnsAllBodiesInOrder()
    {
        var communicationData = new CommunicationData<string>
        {
            Name = "bodies",
            Data =
            [
                new DetailedData<string> { Body = "a" },
                new DetailedData<string> { Body = null },
                new DetailedData<string> { Body = "c" }
            ]
        };

        Assert.That(communicationData.GetBodies(), Is.EqualTo(new[] { "a", null, "c" }));
    }

    [Test]
    public void GetBodiesAs_ReturnsTypedBodies_AndThrowsIndicativelyOnMismatch()
    {
        var typed = new CommunicationData<object>
        {
            Name = "typed-bodies",
            Data = [new DetailedData<object> { Body = "x" }, new DetailedData<object> { Body = "y" }]
        };
        var mismatched = new CommunicationData<object>
        {
            Name = "mismatched-bodies",
            Data = [new DetailedData<object> { Body = "x" }, new DetailedData<object> { Body = 5 }]
        };

        Assert.That(typed.GetBodiesAs<string>(), Is.EqualTo(new[] { "x", "y" }));
        var exception = Assert.Throws<InvalidCastException>(() => mismatched.GetBodiesAs<string>());
        Assert.That(exception!.Message, Does.Contain("index 1"));
        Assert.That(exception.Message, Does.Contain("mismatched-bodies"));
    }

    [Test]
    public void ConvertCommunicationData_UsesOwnSerializationType_ToConvertDeserializedBodies()
    {
        var communicationData = new CommunicationData<object>
        {
            Name = "orders",
            SerializationType = SerializationType.Json,
            Data = [JsonNodeDetailedData("o-1", 10), JsonNodeDetailedData("o-2", 20)]
        };

        var converted = communicationData.ConvertCommunicationData<OrderPayload>();

        Assert.Multiple(() =>
        {
            Assert.That(converted.Name, Is.EqualTo("orders"));
            Assert.That(converted.SerializationType, Is.EqualTo(SerializationType.Json));
            Assert.That(converted.Data, Has.Count.EqualTo(2));
            Assert.That(converted.Data[0].Body!.Id, Is.EqualTo("o-1"));
            Assert.That(converted.Data[1].Body!.Amount, Is.EqualTo(20));
        });
    }

    [Test]
    public void ConvertCommunicationData_WithOverride_UsesGivenSerializationType()
    {
        var communicationData = new CommunicationData<object>
        {
            Name = "override",
            SerializationType = null,
            Data = [JsonNodeDetailedData("o-3", 30)]
        };

        var converted = communicationData.ConvertCommunicationData<OrderPayload>(SerializationType.Json);

        Assert.That(converted.Data[0].Body!.Id, Is.EqualTo("o-3"));
    }

    [Test]
    public void ConvertCommunicationData_WithoutAnySerializationType_FallsBackToPlainCast()
    {
        var communicationData = new CommunicationData<object>
        {
            Name = "plain",
            SerializationType = null,
            Data = [new DetailedData<object> { Body = "text" }]
        };

        var converted = communicationData.ConvertCommunicationData<string>();

        Assert.That(converted.Data[0].Body, Is.EqualTo("text"));
    }

    [Test]
    public void ConvertCommunicationData_FailingItem_ThrowsIndicativeExceptionWithItemIndex()
    {
        var communicationData = new CommunicationData<object>
        {
            Name = "broken",
            SerializationType = SerializationType.Json,
            Data = [JsonNodeDetailedData("ok", 1), new DetailedData<object> { Body = "}{"u8.ToArray() }]
        };

        var exception = Assert.Throws<InvalidCastException>(() =>
            communicationData.ConvertCommunicationData<OrderPayload>());

        Assert.That(exception!.Message, Does.Contain("index 1"));
        Assert.That(exception.Message, Does.Contain("broken"));
        Assert.That(exception.Message, Does.Contain("Json"));
    }

    #endregion

    #region SessionData typed getters

    private static SessionData BuildSessionData() => new()
    {
        Name = "session",
        Inputs =
        [
            new CommunicationData<object>
            {
                Name = "orders_input",
                SerializationType = SerializationType.Json,
                Data = [JsonNodeDetailedData("i-1", 1)]
            }
        ],
        Outputs =
        [
            new CommunicationData<object>
            {
                Name = "orders_output",
                SerializationType = SerializationType.Json,
                Data = [JsonNodeDetailedData("o-1", 10), JsonNodeDetailedData("o-2", 20)]
            }
        ]
    };

    [Test]
    public void GetOutputAs_ReturnsTypedOutputInOneCall()
    {
        var output = BuildSessionData().GetOutputAs<OrderPayload>("orders_output");

        Assert.Multiple(() =>
        {
            Assert.That(output.Name, Is.EqualTo("orders_output"));
            Assert.That(output.Data, Has.Count.EqualTo(2));
            Assert.That(output.Data[0].Body!.Id, Is.EqualTo("o-1"));
            Assert.That(output.Data[1].Body!.Amount, Is.EqualTo(20));
        });
    }

    [Test]
    public void GetInputAs_ReturnsTypedInputInOneCall()
    {
        var input = BuildSessionData().GetInputAs<OrderPayload>("orders_input");

        Assert.That(input.Data[0].Body!.Id, Is.EqualTo("i-1"));
    }

    [Test]
    public void GetOutputBodies_AndGetInputBodies_ReturnTypedBodiesInOneCall()
    {
        var sessionData = BuildSessionData();

        var outputBodies = sessionData.GetOutputBodies<OrderPayload>("orders_output");
        var inputBodies = sessionData.GetInputBodies<OrderPayload>("orders_input");

        Assert.Multiple(() =>
        {
            Assert.That(outputBodies.Select(body => body!.Id), Is.EqualTo(new[] { "o-1", "o-2" }));
            Assert.That(inputBodies.Single()!.Amount, Is.EqualTo(1));
        });
    }

    [Test]
    public void TryGetOutputAs_AndTryGetInputAs_ReportSuccessAndFailureWithoutThrowing()
    {
        var sessionData = BuildSessionData();

        Assert.Multiple(() =>
        {
            Assert.That(sessionData.TryGetOutputAs<OrderPayload>("orders_output", out var output), Is.True);
            Assert.That(output!.Data[0].Body!.Id, Is.EqualTo("o-1"));
            Assert.That(sessionData.TryGetOutputAs<OrderPayload>("missing", out var missingOutput), Is.False);
            Assert.That(missingOutput, Is.Null);
            Assert.That(sessionData.TryGetInputAs<OrderPayload>("orders_input", out var input), Is.True);
            Assert.That(input!.Data, Has.Count.EqualTo(1));
            Assert.That(sessionData.TryGetInputAs<OrderPayload>("missing", out var missingInput), Is.False);
            Assert.That(missingInput, Is.Null);
        });
    }

    [Test]
    public void GetOutputAs_AfterFullSessionDataSerializationRoundTrip_ReturnsTypedBodies()
    {
        // End to end: serialize a session like the runner persists it, deserialize it back,
        // and read typed bodies with the new one-liner
        var serialized = SessionDataSerialization.SerializeSessionData(BuildSessionData());
        var roundTripped = SessionDataSerialization.DeserializeSessionData(serialized);

        var bodies = roundTripped.GetOutputBodies<OrderPayload>("orders_output");

        Assert.That(bodies.Select(body => body!.Id), Is.EqualTo(new[] { "o-1", "o-2" }));
        Assert.That(bodies.Select(body => body!.Amount), Is.EqualTo(new[] { 10, 20 }));
    }

    #endregion
}
