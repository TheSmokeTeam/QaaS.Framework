using System.Collections.Immutable;
using QaaS.Framework.Protocols.ConfigurationObjects.RabbitMq;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Hooks.Generator;
using QaaS.Framework.SDK.Session;
using QaaS.Framework.SDK.Session.CommunicationDataObjects;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;
using QaaS.Framework.Serialization;
using RabbitMQ.Client;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

/// <summary>
/// End-to-end repro of the "generated objects published as json cannot be cast back after consumption"
/// scenario: a custom generator produces Person objects, a publisher serializes them as Json and sends them
/// to a RabbitMQ queue, a consumer reads them back and deserializes them (without a configured specific type,
/// so the bodies are JsonNode) and the user casts the consumed CommunicationData back to Person
/// </summary>
[TestFixture]
public class RabbitMqEndToEndCastIntegrationTests
{
    private const string RabbitMqHostEnvironmentVariableName = "QAAS_RABBITMQ_HOST";
    private const string RabbitMqUsernameEnvironmentVariableName = "QAAS_RABBITMQ_USERNAME";
    private const string RabbitMqPasswordEnvironmentVariableName = "QAAS_RABBITMQ_PASSWORD";

    public class Person
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public string? Email { get; set; }
    }

    public class PersonGeneratorConfiguration
    {
        public int AmountToGenerate { get; set; } = 3;
    }

    private sealed class PersonGenerator : BaseGenerator<PersonGeneratorConfiguration>
    {
        public PersonGenerator() => Configuration = new PersonGeneratorConfiguration();

        public override IEnumerable<Data<object>> Generate(
            IImmutableList<SessionData> sessionDataList,
            IImmutableList<DataSource> dataSourceList
        ) =>
            Enumerable
                .Range(0, Configuration.AmountToGenerate)
                .Select(index => new Data<object>
                {
                    Body = new Person
                    {
                        Name = $"person-{index}",
                        Age = 20 + index,
                        Email = $"person.{index}@qaas.test",
                    },
                });
    }

    [Test]
    public void RabbitMq_GeneratedPeoplePublishedAsJson_CastBackToPeopleAfterConsumption()
    {
        var host = GetRabbitMqHostOrIgnore();
        var username =
            Environment.GetEnvironmentVariable(RabbitMqUsernameEnvironmentVariableName) ?? "admin";
        var password =
            Environment.GetEnvironmentVariable(RabbitMqPasswordEnvironmentVariableName) ?? "admin";
        var exchangeName = $"qaas_cast_repro_{Guid.NewGuid():N}";
        const string routingKey = "person";
        RabbitMqProtocol? reader = null;
        RabbitMqProtocol? sender = null;

        try
        {
            DeclareExchange(host, username, password, exchangeName);
            reader = new RabbitMqProtocol(
                new RabbitMqReaderConfig
                {
                    Host = host,
                    Username = username,
                    Password = password,
                    ExchangeName = exchangeName,
                    RoutingKey = routingKey,
                },
                Globals.Logger
            );
            sender = new RabbitMqProtocol(
                new RabbitMqSenderConfig
                {
                    Host = host,
                    Username = username,
                    Password = password,
                    ExchangeName = exchangeName,
                    RoutingKey = routingKey,
                },
                Globals.Logger
            );
            reader.Connect();
            sender.Connect();

            // Publisher side: custom generator output serialized as Json and published to the queue
            var generator = new PersonGenerator();
            var generatedPeople = generator
                .Generate(ImmutableList<SessionData>.Empty, ImmutableList<DataSource>.Empty)
                .ToList();
            var producedCommunicationData = new CommunicationData<object>
            {
                Name = "people",
                SerializationType = SerializationType.Json,
                Data = generatedPeople
                    .Select(generated => new DetailedData<object>
                    {
                        Body = generated.Body,
                        MetaData = generated.MetaData,
                    })
                    .ToList(),
            };
            var serializedCommunicationData = SessionDataSerialization.SerializeCommunicationData(
                producedCommunicationData
            );
            foreach (var serializedItem in serializedCommunicationData.Data)
                sender.Send(new Data<object> { Body = serializedItem.Body });

            // Consumer side: read the raw bytes back and deserialize them without a configured specific
            // type, exactly like a runner consumer that has no SpecificTypeConfig for the queue
            var consumedItems = generatedPeople
                .Select(_ =>
                {
                    var consumed = reader.Read(TimeSpan.FromSeconds(10));
                    Assert.That(
                        consumed,
                        Is.Not.Null,
                        "Expected a message from the queue but got none"
                    );
                    return new SerializedDetailedData
                    {
                        Body = (byte[]?)consumed!.Body,
                        MetaData = consumed.MetaData,
                        Timestamp = consumed.Timestamp,
                    };
                })
                .ToList();
            var consumedCommunicationData = SessionDataSerialization.DeserializeCommunicationData(
                new SerializedCommunicationData
                {
                    Name = "people_consumed",
                    SerializationType = SerializationType.Json,
                    Data = consumedItems,
                }
            );

            // The user-facing step that used to throw InvalidCastException ("cannot cast JsonNode to Person")
            var consumedPeople = consumedCommunicationData.CastCommunicationData<Person>();

            var expectedPeople = generatedPeople
                .Select(generated => (Person)generated.Body!)
                .ToList();
            Assert.Multiple(() =>
            {
                Assert.That(consumedPeople.Data, Has.Count.EqualTo(expectedPeople.Count));
                Assert.That(
                    consumedPeople.Data.Select(item => item.Body?.Name),
                    Is.EquivalentTo(expectedPeople.Select(person => person.Name))
                );
                Assert.That(
                    consumedPeople.Data.Select(item => item.Body?.Age),
                    Is.EquivalentTo(expectedPeople.Select(person => person.Age))
                );
                Assert.That(
                    consumedPeople.Data.Select(item => item.Body?.Email),
                    Is.EquivalentTo(expectedPeople.Select(person => person.Email))
                );
            });
        }
        finally
        {
            TryDisconnect(sender);
            TryDisconnect(reader);
            TryDeleteExchange(host, username, password, exchangeName);
        }
    }

    private static string GetRabbitMqHostOrIgnore()
    {
        var host = Environment.GetEnvironmentVariable(RabbitMqHostEnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(host))
            Assert.Ignore(
                $"Set the `{RabbitMqHostEnvironmentVariableName}` (and optionally"
                    + $" `{RabbitMqUsernameEnvironmentVariableName}` and"
                    + $" `{RabbitMqPasswordEnvironmentVariableName}`) environment variables to run"
                    + " RabbitMQ integration tests"
            );
        return host!;
    }

    private static void DeclareExchange(
        string host,
        string username,
        string password,
        string exchangeName
    )
    {
        using var connection = CreateConnection(host, username, password);
        using var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
        channel
            .ExchangeDeclareAsync(
                exchangeName,
                ExchangeType.Direct,
                durable: false,
                autoDelete: false
            )
            .GetAwaiter()
            .GetResult();
    }

    private static void TryDeleteExchange(
        string host,
        string username,
        string password,
        string exchangeName
    )
    {
        try
        {
            using var connection = CreateConnection(host, username, password);
            using var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
            channel.ExchangeDeleteAsync(exchangeName).GetAwaiter().GetResult();
        }
        catch
        {
            // Best effort cleanup, the exchange is transient anyway
        }
    }

    private static IConnection CreateConnection(string host, string username, string password) =>
        new ConnectionFactory
        {
            HostName = host,
            UserName = username,
            Password = password,
        }
            .CreateConnectionAsync()
            .GetAwaiter()
            .GetResult();

    private static void TryDisconnect(RabbitMqProtocol? protocol)
    {
        try
        {
            protocol?.Disconnect();
        }
        catch
        {
            // Best effort cleanup of test connections
        }
    }
}
