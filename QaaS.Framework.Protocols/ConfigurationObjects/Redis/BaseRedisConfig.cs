using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using StackExchange.Redis;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Redis;

public record BaseRedisConfig
{
    [Required,
     Description("List of the redis hostnames (each hostname should contain the port too for example: - 'host1:8080'")]
    public string[]? HostNames { get; set; }

    [Description("User for the redis server"), DefaultValue(null)]
    public string? Username { get; set; } = null;

    [Description("Password for the redis server"), DefaultValue(null)]
    public string? Password { get; set; } = null;

    [Description("If true, connect will not create connection while no servers are available"), DefaultValue(true)]
    public bool AbortOnConnectFail { get; set; } = true;

    [Description("The number of times to repeat connect attempts during initial connect"), DefaultValue(3)]
    public int ConnectRetry { get; set; } = 3;

    [Description("Identification for the connection within redis"), DefaultValue(null)]
    public string? ClientName { get; set; } = null;

    [Description("Time(ms) to allow for asynchronous operations"), DefaultValue(5000)]
    public int AsyncTimeout { get; set; } = 5000;

    [Description("Specifies that SSL encryption should be used"), DefaultValue(false)]
    public bool Ssl { get; set; } = false;

    [Description("Enforces a preticular SSL host identity on the server's certificate"), DefaultValue(null)]
    public string? SslHost { get; set; } = null;

    [Description("Time (seconds) at which to send a message to help keep alive"), DefaultValue(60)]
    public int KeepAlive { get; set; } = 60;

    public ConfigurationOptions CreateRedisConfigurationOptions()
    {
        var config = new ConfigurationOptions
        {
            User = Username,
            Password = Password,
            AbortOnConnectFail = AbortOnConnectFail,
            ConnectRetry = ConnectRetry,
            KeepAlive = KeepAlive,
            ClientName = ClientName,
            AsyncTimeout = AsyncTimeout,
            Ssl = Ssl,
            SslHost = SslHost
        };
        foreach (var host in HostNames!)
            config.EndPoints.Add(host);
        return config;
    }
}