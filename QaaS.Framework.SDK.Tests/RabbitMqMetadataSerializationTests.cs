using System.Text.Json;
using QaaS.Framework.SDK.Session.MetaDataObjects;

namespace QaaS.Framework.SDK.Tests;

[TestFixture]
public class RabbitMqMetadataSerializationTests
{
    [Test]
    public void RabbitMqMetadata_DefaultSerialization_OmitsUnsetOptionalFields()
    {
        var metadata = new RabbitMq
        {
            RoutingKey = "/"
        };

        var json = JsonSerializer.Serialize(metadata);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"RoutingKey\":\"/\""));
            Assert.That(json, Does.Not.Contain("\"Headers\""));
            Assert.That(json, Does.Not.Contain("\"Expiration\""));
            Assert.That(json, Does.Not.Contain("\"ContentType\""));
            Assert.That(json, Does.Not.Contain("\"Type\""));
        });
    }

    [Test]
    public void RabbitMqMetadata_DefaultSerialization_PreservesConfiguredOptionalFields()
    {
        var metadata = new RabbitMq
        {
            RoutingKey = "/",
            Headers = new Dictionary<string, object?> { ["x-trace-id"] = "abc" },
            Expiration = "2000",
            ContentType = "application/json",
            Type = "event"
        };

        var json = JsonSerializer.Serialize(metadata);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"RoutingKey\":\"/\""));
            Assert.That(json, Does.Contain("\"Headers\""));
            Assert.That(json, Does.Contain("\"Expiration\":\"2000\""));
            Assert.That(json, Does.Contain("\"ContentType\":\"application/json\""));
            Assert.That(json, Does.Contain("\"Type\":\"event\""));
        });
    }
}
