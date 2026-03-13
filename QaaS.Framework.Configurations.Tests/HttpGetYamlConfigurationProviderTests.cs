using System.Net;
using System.Net.Sockets;
using System.Text;
using QaaS.Framework.Configurations.ConfigurationProviders;
using QaaS.Framework.Configurations.CustomExceptions;

namespace QaaS.Framework.Configurations.Tests;

[TestFixture]
public class HttpGetYamlConfigurationProviderTests
{
    [Test]
    public void Load_WhenYamlIsAvailable_LoadsConfigurationValues()
    {
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        var serverTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            context.Response.StatusCode = 200;
            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("root:\n  child: value"));
            context.Response.Close();
        });

        var provider = new HttpGetYamlConfigurationProvider($"http://127.0.0.1:{port}/config.yaml");

        provider.Load();
        serverTask.GetAwaiter().GetResult();

        Assert.That(provider.TryGet("root:child", out var value), Is.True);
        Assert.That(value, Is.EqualTo("value"));
    }

    [Test]
    public void Load_WhenRequestFails_ThrowsCouldNotFindConfigurationException()
    {
        var closedPort = GetFreeTcpPort();
        var provider = new HttpGetYamlConfigurationProvider(
            $"http://127.0.0.1:{closedPort}/missing.yaml",
            TimeSpan.FromMilliseconds(100));

        Assert.Throws<CouldNotFindConfigurationException>(() => provider.Load());
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
