using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using IBM.WMQ;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QaaS.Framework.Configurations.CustomExceptions;
using QaaS.Framework.Protocols.ConfigurationObjects;
using QaaS.Framework.Protocols.ConfigurationObjects.Http;
using QaaS.Framework.Protocols.ConfigurationObjects.Prometheus;
using QaaS.Framework.Protocols.ConfigurationObjects.Redis;
using QaaS.Framework.Protocols.ConfigurationObjects.S3;
using QaaS.Framework.Protocols.ConfigurationObjects.Socket;
using QaaS.Framework.Protocols.ConfigurationObjects.Sql;
using QaaS.Framework.Protocols.Extentions;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.Protocols.Protocols.Factories;
using QaaS.Framework.Protocols.Utils;
using QaaS.Framework.Protocols.Utils.S3Utils;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class ProtocolBehaviorTests
{
    private sealed class UnsupportedReaderConfig : IReaderConfig;
    private sealed class UnsupportedSenderConfig : ISenderConfig;
    private sealed class UnsupportedTransactorConfig : ITransactorConfig;
    private sealed class UnsupportedFetcherConfig : IFetcherConfig;

    private sealed class StubPrometheusProtocol(PrometheusFetcherConfig fetcherConfig, string body)
        : PrometheusProtocol(fetcherConfig, Globals.Logger)
    {
        public string? LastQueryUri { get; private set; }

        protected override string HttpGetResultBodyAsString(string queryRequestUri)
        {
            LastQueryUri = queryRequestUri;
            return body;
        }
    }

    [Test]
    public void ObjectNameGenerator_GeneratesExpectedNames()
    {
        var sequential = new ObjectNameGenerator(ObjectNamingGeneratorType.GrowingNumericalSeries, "pref-");
        var guid = new ObjectNameGenerator(ObjectNamingGeneratorType.RandomGuid, "pref-");
        var invalid = new ObjectNameGenerator((ObjectNamingGeneratorType)999, "x-");

        Assert.Multiple(() =>
        {
            Assert.That(sequential.GenerateObjectName(), Is.EqualTo("pref-0"));
            Assert.That(sequential.GenerateObjectName(), Is.EqualTo("pref-1"));
            Assert.That(guid.GenerateObjectName(), Does.StartWith("pref-"));
            Assert.Throws<ArgumentOutOfRangeException>(() => invalid.GenerateObjectName());
        });
    }

    [Test]
    public void S3StorageClassEnumExtension_MapsAllEnumValues()
    {
        foreach (var value in Enum.GetValues<S3StorageClassEnum>())
        {
            var mapped = value.GetS3StorageClassFromEnum();
            Assert.That(mapped, Is.Not.Null);
        }
    }

    [Test]
    public void RunS3OperationWithRetryMechanism_RetriesTooManyRequestsAndStopsOnLimit()
    {
        var attempts = 0;
        var result = S3Extentions.RunS3OperationWithRetryMechanism(() =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new AmazonS3Exception("retry") { ErrorCode = "TooManyRequests" };
                }

                return 7;
            },
            "retry-test",
            maxRetryCount: 5,
            logger: NullLogger.Instance);

        Assert.That(result, Is.EqualTo(7));
        Assert.That(attempts, Is.EqualTo(3));

        Assert.Throws<AmazonS3Exception>(() =>
            S3Extentions.RunS3OperationWithRetryMechanism<int>(
                () => throw new AmazonS3Exception("fail") { ErrorCode = "AccessDenied" },
                "no-retry"));

        Assert.Throws<AmazonS3Exception>(() =>
        {
            var calls = 0;
            _ = S3Extentions.RunS3OperationWithRetryMechanism<int>(
                () =>
                {
                    calls++;
                    throw new AmazonS3Exception("limited") { ErrorCode = "TooManyRequests" };
                },
                "limit",
                maxRetryCount: 1);
        });
    }

    [Test]
    public void PrometheusProtocol_Collect_ParsesMatrixResult()
    {
        var config = new PrometheusFetcherConfig
        {
            Url = "http://prometheus.local",
            Expression = "up",
            SampleIntervalMs = 1000,
            TimeoutMs = 1000
        };
        const string body = """
                            {"status":"success","data":{"resultType":"matrix","result":[{"metric":{"job":"api"},"values":[[1710000000,"1"],[1710000001,"2"]]}]}}
                            """;
        var protocol = new StubPrometheusProtocol(config, body);

        var result = protocol.Collect(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].Timestamp!.Value, Is.LessThanOrEqualTo(result[1].Timestamp!.Value));
            Assert.That(result[0].Body!.ToString(), Does.Contain("metric"));
            Assert.That(protocol.LastQueryUri, Does.Contain("/api/v1/query_range"));
            Assert.That(protocol.GetSerializationType(), Is.EqualTo(QaaS.Framework.Serialization.SerializationType.Json));
        });
    }

    [Test]
    public void PrometheusProtocol_Collect_ThrowsOnInvalidResult()
    {
        var config = new PrometheusFetcherConfig
        {
            Url = "http://prometheus.local",
            Expression = "up"
        };

        var invalidStatus = new StubPrometheusProtocol(config,
            """{"status":"error","data":{"resultType":"matrix","result":[]}}""");
        Assert.Throws<Exception>(() => invalidStatus.Collect(DateTime.UtcNow, DateTime.UtcNow).ToList());

        var invalidType = new StubPrometheusProtocol(config,
            """{"status":"success","data":{"resultType":"vector","result":[]}}""");
        Assert.Throws<ArgumentOutOfRangeException>(() => invalidType.Collect(DateTime.UtcNow, DateTime.UtcNow).ToList());

        var invalidTimestamp = new StubPrometheusProtocol(config,
            """{"status":"success","data":{"resultType":"matrix","result":[{"metric":{"job":"api"},"values":[["bad","1"]]}]}}""");
        Assert.Throws<ArgumentException>(() => invalidTimestamp.Collect(DateTime.UtcNow, DateTime.UtcNow).ToList());
    }

    [Test]
    public void HttpProtocol_Transact_SuccessfullySendsAndReceives()
    {
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var serverTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            context.Response.StatusCode = (int)HttpStatusCode.Created;
            context.Response.Headers.Add("X-Resp", "ok");
            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("pong"));
            context.Response.Close();
        });

        var protocol = new HttpProtocol(new HttpTransactorConfig
        {
            Method = HttpMethods.Post,
            BaseAddress = "http://127.0.0.1",
            Port = port,
            Route = "/",
            Headers = new Dictionary<string, string> { ["Content-Type"] = "application/octet-stream" },
            RequestHeaders = new Dictionary<string, string> { ["X-Req"] = "1" },
            Retries = 1
        }, Globals.Logger, TimeSpan.FromSeconds(5));

        var result = protocol.Transact(new Data<object>
        {
            Body = Encoding.UTF8.GetBytes("ping"),
            MetaData = new MetaData
            {
                Http = new Http
                {
                    Headers = new Dictionary<string, string> { ["X-Data"] = "d" }
                }
            }
        });

        serverTask.GetAwaiter().GetResult();
        var response = result.Item2!;

        Assert.Multiple(() =>
        {
            Assert.That(result.Item1.Body, Is.TypeOf<byte[]>());
            Assert.That(response.Body, Is.TypeOf<byte[]>());
            Assert.That(Encoding.UTF8.GetString((byte[])response.Body!), Is.EqualTo("pong"));
            Assert.That(response.MetaData?.Http?.StatusCode, Is.EqualTo(201));
            Assert.That(response.MetaData?.Http?.ResponseHeaders, Contains.Key("X-Resp"));
        });
    }

    [Test]
    public void HttpProtocol_Transact_WhenNoResponse_Throws()
    {
        var closedPort = GetFreeTcpPort();
        var protocol = new HttpProtocol(new HttpTransactorConfig
        {
            Method = HttpMethods.Get,
            BaseAddress = "http://127.0.0.1",
            Port = closedPort,
            Route = "/",
            Retries = 1
        }, Globals.Logger, TimeSpan.FromMilliseconds(200));

        Assert.Throws<NullReferenceException>(() =>
            protocol.Transact(new Data<object> { Body = Array.Empty<byte>() }));
    }

    [Test]
    public void HttpProtocol_AppliesJwtAuthConfiguration()
    {
        var basicAuthProtocol = new HttpProtocol(new HttpTransactorConfig
        {
            Method = HttpMethods.Get,
            BaseAddress = "http://127.0.0.1",
            Port = 80,
            JwtAuth = new JwtAuthConfig
            {
                BuildJwtConfig = false,
                HttpAuthScheme = HttpAuthorizationSchemes.Basic,
                Secret = "token"
            }
        }, Globals.Logger, TimeSpan.FromSeconds(1));

        var jwtProtocol = new HttpProtocol(new HttpTransactorConfig
        {
            Method = HttpMethods.Get,
            BaseAddress = "http://127.0.0.1",
            Port = 80,
            JwtAuth = new JwtAuthConfig
            {
                BuildJwtConfig = true,
                Secret = "secret",
                Claims = new Dictionary<string, string> { ["sub"] = "user" }
            }
        }, Globals.Logger, TimeSpan.FromSeconds(1));

        var httpClientField = typeof(HttpProtocol).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var basicHttpClient = (HttpClient)httpClientField.GetValue(basicAuthProtocol)!;
        var jwtHttpClient = (HttpClient)httpClientField.GetValue(jwtProtocol)!;

        Assert.Multiple(() =>
        {
            Assert.That(basicHttpClient.DefaultRequestHeaders.Authorization!.Scheme, Is.EqualTo("Basic"));
            Assert.That(basicHttpClient.DefaultRequestHeaders.Authorization.Parameter, Is.EqualTo("token"));
            Assert.That(jwtHttpClient.DefaultRequestHeaders.Authorization!.Scheme, Is.EqualTo("Bearer"));
            Assert.That(jwtHttpClient.DefaultRequestHeaders.Authorization.Parameter, Is.Not.EqualTo("secret"));
        });
    }

    [Test]
    public void HttpExtensions_HierarchicalClaimsParsingAndValidation()
    {
        var getClaimsMethod = typeof(HttpProtocol).Assembly
            .GetType("QaaS.Framework.Protocols.Extentions.HttpExtentions")!
            .GetMethod("GetClaimsFromHierarchicalClaims", BindingFlags.Static | BindingFlags.NonPublic)!;

        var claims = (Dictionary<string, object>)getClaimsMethod.Invoke(null, ["a: 1\nb: two"])!;
        Assert.That(claims["a"].ToString(), Is.EqualTo("1"));

        var exception = Assert.Throws<TargetInvocationException>(() =>
            getClaimsMethod.Invoke(null, ["not: [yaml"]));
        Assert.That(exception!.InnerException, Is.TypeOf<InvalidConfigurationsException>());
    }

    [Test]
    public void BaseRedisConfig_CreateRedisConfigurationOptions_BuildsEndpoints()
    {
        var config = new BaseRedisConfig
        {
            HostNames = ["localhost:6379", "localhost:6380"],
            Username = "u",
            Password = "p",
            AbortOnConnectFail = false,
            ConnectRetry = 2,
            KeepAlive = 5,
            ClientName = "client",
            AsyncTimeout = 123,
            Ssl = true,
            SslHost = "ssl-host"
        };

        var options = config.CreateRedisConfigurationOptions();

        Assert.Multiple(() =>
        {
            Assert.That(options.EndPoints, Has.Count.EqualTo(2));
            Assert.That(options.User, Is.EqualTo("u"));
            Assert.That(options.Password, Is.EqualTo("p"));
            Assert.That(options.AbortOnConnectFail, Is.False);
            Assert.That(options.Ssl, Is.True);
            Assert.That(options.SslHost, Is.EqualTo("ssl-host"));
        });
    }

    [Test]
    public void SocketSenderConfig_NagleValidation_RequiresTcp()
    {
        var config = new SocketSenderConfig
        {
            Host = "localhost",
            Port = 1000,
            ProtocolType = ProtocolType.Udp,
            NagleAlgorithm = true
        };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(config, new ValidationContext(config), results, true);

        Assert.That(valid, Is.False);
        Assert.That(results.Single().ErrorMessage, Does.Contain(nameof(ProtocolType.Tcp)));
    }

    [Test]
    public void Factories_CreateSupportedAndUnsupportedProtocols()
    {
        var logger = NullLogger.Instance;

        var (sender, chunkSender) = SenderFactory.CreateSender(false, new PostgreSqlSenderConfig
        {
            ConnectionString = "Host=localhost;Username=u;Password=p;Database=db",
            TableName = "tbl"
        }, logger, dataFilter: null);

        var (sender2, chunkSender2) = SenderFactory.CreateSender(true, new PostgreSqlSenderConfig
        {
            ConnectionString = "Host=localhost;Username=u;Password=p;Database=db",
            TableName = "tbl"
        }, logger, dataFilter: null);

        var (reader, chunkReader) = ReaderFactory.CreateReader(new PostgreSqlReaderConfig
        {
            ConnectionString = "Host=localhost;Username=u;Password=p;Database=db",
            TableName = "tbl",
            InsertionTimeField = "created_at"
        }, logger, dataFilter: null);

        var transactor = TransactorFactory.CreateTransactor(new HttpTransactorConfig
        {
            Method = HttpMethods.Get,
            BaseAddress = "http://127.0.0.1",
            Port = 80
        }, logger, TimeSpan.FromSeconds(1));

        var fetcher = FetcherFactory.CreateFetcher(new PrometheusFetcherConfig
        {
            Url = "http://prometheus.local",
            Expression = "up"
        }, logger);

        Assert.Multiple(() =>
        {
            Assert.That(sender, Is.Not.Null);
            Assert.That(chunkSender, Is.Null);
            Assert.That(sender2, Is.Null);
            Assert.That(chunkSender2, Is.Not.Null);
            Assert.That(reader, Is.Null);
            Assert.That(chunkReader, Is.Not.Null);
            Assert.That(transactor, Is.TypeOf<HttpProtocol>());
            Assert.That(fetcher, Is.TypeOf<PrometheusProtocol>());

            Assert.Throws<InvalidOperationException>(() =>
                ReaderFactory.CreateReader(new UnsupportedReaderConfig(), logger, null));
            Assert.Throws<InvalidOperationException>(() =>
                SenderFactory.CreateSender(false, new UnsupportedSenderConfig(), logger, null));
            Assert.Throws<InvalidOperationException>(() =>
                TransactorFactory.CreateTransactor(new UnsupportedTransactorConfig(), logger, TimeSpan.FromSeconds(1)));
            Assert.Throws<InvalidOperationException>(() =>
                FetcherFactory.CreateFetcher(new UnsupportedFetcherConfig(), logger));
        });
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
