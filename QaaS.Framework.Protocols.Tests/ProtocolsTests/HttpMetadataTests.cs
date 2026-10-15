using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using QaaS.Framework.Protocols.ConfigurationObjects.Http;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class HttpMetadataTests
{
    private static HttpProtocol Protocol(
        string address = "http://localhost/api",
        int? port = 8080
    ) =>
        new(
            new HttpTransactorConfig
            {
                BaseAddress = address,
                Port = port,
                Method = HttpMethods.Post,
            },
            NullLogger.Instance,
            TimeSpan.FromSeconds(2)
        );

    private static HttpRequestMessage Request(
        HttpProtocol protocol,
        Http metadata,
        string fallback = "http://localhost:8080/api/users/{id}?fixed=1"
    )
    {
        var method =
            typeof(HttpProtocol).GetMethod(
                "CreateRequest",
                BindingFlags.Instance | BindingFlags.NonPublic
            ) ?? throw new InvalidOperationException("Request factory missing");
        try
        {
            return method.Invoke(
                    protocol,
                    [
                        new Data<byte[]>
                        {
                            Body = [],
                            MetaData = new MetaData { Http = metadata },
                        },
                        fallback,
                    ]
                ) as HttpRequestMessage
                ?? throw new InvalidOperationException("No request");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System
                .Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException)
                .Throw();
            throw;
        }
    }

    [Test]
    public void PathParameters_AreEncodedOnce_AndDoNotReplaceQueryTemplates()
    {
        using var protocol = Protocol();
        using var request = Request(
            protocol,
            new Http { PathParameters = new Dictionary<string, string> { ["id"] = "a/b ?#%שלום" } },
            "http://localhost:8080/users/{id}?literal={id}"
        );
        Assert.That(
            request.RequestUri?.AbsoluteUri,
            Is.EqualTo(
                "http://localhost:8080/users/a%2Fb%20%3F%23%25%D7%A9%D7%9C%D7%95%D7%9D?literal=%7Bid%7D"
            )
        );
    }

    [Test]
    public void MissingPathParameter_FailsBeforeSending()
    {
        using var protocol = Protocol();
        Assert.That(
            () => Request(protocol, new Http()),
            Throws.ArgumentException.With.Message.Contains("id")
        );
    }

    [TestCase("/orders", "http://localhost:8080/api/orders")]
    [TestCase("orders", "http://localhost:8080/api/orders")]
    [TestCase("", "http://localhost:8080/api")]
    [TestCase("?x=1", "http://localhost:8080/api?x=1")]
    public void ItemRoute_AppendsToBasePath_WithOneBoundarySlash(string route, string expected)
    {
        using var protocol = Protocol();
        using var request = Request(protocol, new Http { Route = route });
        Assert.That(request.RequestUri?.AbsoluteUri, Is.EqualTo(expected));
    }

    [TestCase("http://localhost:1234/api", null, "http://localhost:1234/api/orders")]
    [TestCase("http://localhost:1234/api", 8080, "http://localhost:8080/api/orders")]
    public void Port_IsAppliedToAuthority(string address, int? port, string expected)
    {
        using var protocol = Protocol(address, port);
        using var request = Request(protocol, new Http { Route = "orders" });
        Assert.That(request.RequestUri?.AbsoluteUri, Is.EqualTo(expected));
    }

    [Test]
    public void UriOverride_RemainsAuthoritative_AndQueryPairsAppendBeforeFragment()
    {
        using var protocol = Protocol();
        using var request = Request(
            protocol,
            new Http
            {
                Uri = new Uri("http://other/users/42?x=1#section"),
                Route = "ignored/{missing}",
                PathParameters = new Dictionary<string, string> { ["id"] = "ignored" },
                QueryParameters = new Dictionary<string, string> { ["x"] = "a &b" },
            }
        );
        Assert.That(
            request.RequestUri?.AbsoluteUri,
            Is.EqualTo("http://other/users/42?x=1&x=a%20%26b#section")
        );
    }

    [Test]
    public void ExistingQueryData_PreservesLeadingQuestionMark()
    {
        using var protocol = Protocol();
        using var request = Request(
            protocol,
            new Http
            {
                Uri = new Uri("http://localhost/p??x=1"),
                QueryParameters = new Dictionary<string, string> { ["q"] = "v" },
            }
        );
        Assert.That(request.RequestUri?.AbsoluteUri, Is.EqualTo("http://localhost/p??x=1&q=v"));
    }

    [TestCase(".")]
    [TestCase("..")]
    public void DotPathParameters_FailRatherThanNavigate(string value)
    {
        using var protocol = Protocol();
        Assert.That(
            () =>
                Request(
                    protocol,
                    new Http { PathParameters = new Dictionary<string, string> { ["id"] = value } }
                ),
            Throws.ArgumentException
        );
    }

    [Test]
    public void RelativeUri_IsResolvedAgainstConfiguredBaseDirectory()
    {
        using var protocol = Protocol();
        using var request = Request(
            protocol,
            new Http { Uri = new Uri("orders?id=1", UriKind.Relative) }
        );
        Assert.That(
            request.RequestUri?.AbsoluteUri,
            Is.EqualTo("http://localhost:8080/api/orders?id=1")
        );
    }

    [TestCase("https://other/orders")]
    [TestCase("//other/orders")]
    public void Route_CannotReplaceAuthority(string route)
    {
        using var protocol = Protocol();
        Assert.That(() => Request(protocol, new Http { Route = route }), Throws.ArgumentException);
    }

    [Test]
    public void Method_IsPerItem_AndDoesNotLeakToFollowingRequest()
    {
        using var protocol = Protocol();
        using var patch = Request(protocol, new Http { Route = "orders", Method = "PATCH" });
        using var normal = Request(protocol, new Http { Route = "orders" });
        Assert.Multiple(() =>
        {
            Assert.That(patch.Method, Is.EqualTo(HttpMethod.Patch));
            Assert.That(normal.Method, Is.EqualTo(HttpMethod.Post));
        });
    }

    [TestCase(HttpMethods.Head, "HEAD")]
    [TestCase(HttpMethods.Patch, "PATCH")]
    [TestCase(HttpMethods.Options, "OPTIONS")]
    public void ConfiguredMethods_AreSupported(HttpMethods method, string expected)
    {
        using var protocol = Protocol();
        protocol.Method = method;
        using var request = Request(protocol, new Http { Route = "orders" });
        Assert.That(request.Method.Method, Is.EqualTo(expected));
    }

    [Test]
    public void MetadataAndHeaderDictionaries_AreNotMutated()
    {
        using var protocol = Protocol();
        var metadata = new Http
        {
            Route = "users/{id}",
            PathParameters = new Dictionary<string, string> { ["id"] = "42" },
            QueryParameters = new Dictionary<string, string> { ["q"] = "a b" },
            RequestHeaders = new Dictionary<string, string> { ["X-Test"] = "value" },
        };
        Parallel.For(
            0,
            20,
            _ =>
            {
                using var request = Request(protocol, metadata);
                Assert.That(
                    request.RequestUri?.AbsoluteUri,
                    Is.EqualTo("http://localhost:8080/api/users/42?q=a%20b")
                );
            }
        );
        Assert.Multiple(() =>
        {
            Assert.That(metadata.Route, Is.EqualTo("users/{id}"));
            Assert.That(metadata.Uri, Is.Null);
            Assert.That(metadata.PathParameters["id"], Is.EqualTo("42"));
        });
    }
}
