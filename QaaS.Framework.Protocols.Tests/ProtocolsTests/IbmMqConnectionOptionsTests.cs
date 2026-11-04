using IBM.WMQ;
using QaaS.Framework.Protocols.ConfigurationObjects.IbmMq;
using QaaS.Framework.Protocols.Protocols;

namespace QaaS.Framework.Protocols.Tests.ProtocolsTests;

[TestFixture]
public class IbmMqConnectionOptionsTests
{
    [Test]
    public void OmittedOptions_KeepLegacyConnectionProperties()
    {
        var properties = IbmMqProtocol.CreateConnectionProperties(new IbmMqReaderConfig());
        Assert.That(properties.Count, Is.EqualTo(3));
        Assert.That(properties.ContainsKey(MQC.USE_MQCSP_AUTHENTICATION_PROPERTY), Is.False);
    }

    [Test]
    public void ExplicitCredentialsAndTls_AreMappedToTheNativeClient()
    {
        var properties = IbmMqProtocol.CreateConnectionProperties(
            new IbmMqReaderConfig
            {
                Username = "test-user",
                Password = "test-only",
                SslCipherSpec = "TLS_AES_128_GCM_SHA256",
                SslKeyRepository = "*USER",
                SslPeerName = "CN=test-server",
            }
        );
        Assert.That(properties[MQC.USER_ID_PROPERTY], Is.EqualTo("test-user"));
        Assert.That(properties[MQC.PASSWORD_PROPERTY], Is.EqualTo("test-only"));
        Assert.That(properties[MQC.USE_MQCSP_AUTHENTICATION_PROPERTY], Is.True);
        Assert.That(properties[MQC.SSL_CIPHER_SPEC_PROPERTY], Is.EqualTo("TLS_AES_128_GCM_SHA256"));
        Assert.That(properties[MQC.SSL_CERT_STORE_PROPERTY], Is.EqualTo("*USER"));
        Assert.That(properties[MQC.SSL_PEER_NAME_PROPERTY], Is.EqualTo("CN=test-server"));
    }
}
