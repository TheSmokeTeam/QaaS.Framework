using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using QaaS.Framework.Protocols.ConfigurationObjects.Http;
using QaaS.Framework.Protocols.Protocols;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class HttpPatchTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void CreateRequest_Patch_PreservesBodyAndContentHeaders(bool useMetadata)
    {
        using var protocol = new HttpProtocol(
            new HttpTransactorConfig
            {
                Method = useMetadata ? HttpMethods.Post : HttpMethods.Patch,
                BaseAddress = "http://localhost",
                Headers = new() { ["Content-Type"] = "application/json-patch+json" },
            },
            Globals.Logger,
            TimeSpan.FromSeconds(1)
        );
        var payload = Encoding.UTF8.GetBytes("[{\"op\":\"remove\",\"path\":\"/name\"}]");
        var factory = typeof(HttpProtocol).GetMethod(
            "CreateRequest",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        using var request = (HttpRequestMessage)
            factory.Invoke(
                protocol,
                [
                    new Data<byte[]>
                    {
                        Body = payload,
                        MetaData = useMetadata
                            ? new MetaData { Http = new Http { Method = "PATCH" } }
                            : null,
                    },
                    "http://localhost/items/42",
                ]
            )!;
        Assert.Multiple(() =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Patch));
            Assert.That(
                request.Content?.ReadAsByteArrayAsync().GetAwaiter().GetResult(),
                Is.EqualTo(payload)
            );
            Assert.That(
                request.Content?.Headers.ContentType?.MediaType,
                Is.EqualTo("application/json-patch+json")
            );
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Transact_Patch_SendsPayloadAndHeaders_AndCapturesResponse(bool useMetadata)
    {
        var port = GetFreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        const string body = "{\"enabled\":true}";
        var server = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(10));
            using var reader = new StreamReader(context.Request.InputStream);
            var receivedBody = await reader.ReadToEndAsync();
            var request = (
                context.Request.HttpMethod,
                context.Request.RawUrl,
                context.Request.ContentType,
                context.Request.Headers["X-Request"],
                receivedBody
            );
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(body));
            context.Response.Close();
            return request;
        });
        using var protocol = new HttpProtocol(
            new HttpTransactorConfig
            {
                Method = useMetadata ? HttpMethods.Post : HttpMethods.Patch,
                BaseAddress = "http://127.0.0.1",
                Port = port,
                Route = "/items/42",
                Headers = new() { ["Content-Type"] = "application/merge-patch+json" },
                RequestHeaders = new() { ["X-Request"] = "patch-test" },
            },
            Globals.Logger,
            TimeSpan.FromSeconds(5)
        );

        var result = protocol.Transact(
            new Data<object>
            {
                Body = Encoding.UTF8.GetBytes(body),
                MetaData = useMetadata
                    ? new MetaData { Http = new Http { Method = "PATCH" } }
                    : null,
            }
        );
        var request = await server.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Multiple(() =>
        {
            Assert.That(request.HttpMethod, Is.EqualTo("PATCH"));
            Assert.That(request.RawUrl, Is.EqualTo("/items/42"));
            Assert.That(request.ContentType, Is.EqualTo("application/merge-patch+json"));
            Assert.That(request.Item4, Is.EqualTo("patch-test"));
            Assert.That(request.receivedBody, Is.EqualTo(body));
            Assert.That(result.Item2?.MetaData?.Http?.StatusCode, Is.EqualTo(200));
            Assert.That(result.Item2?.Body, Is.EqualTo(Encoding.UTF8.GetBytes(body)));
        });
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
