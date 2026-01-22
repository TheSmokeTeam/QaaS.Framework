using Microsoft.Extensions.Logging;
using QaaS.Framework.Protocols.ConfigurationObjects.Sftp;
using QaaS.Framework.Protocols.Utils;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.Serialization;
using Renci.SshNet;


namespace QaaS.Framework.Protocols.Protocols;

public class SftpProtocol : ISender
{
    private readonly ILogger _logger;
    private readonly SftpSenderConfig _senderConfig;
    
    private readonly ISftpClient _producer;
    private ObjectNameGenerator Generator { get; set; }
    
    public SftpProtocol(SftpSenderConfig configuration, ILogger logger)
    {
        _logger = logger;
        _senderConfig = configuration;
        Generator = new ObjectNameGenerator(_senderConfig.NamingType, _senderConfig.Prefix);
        _producer = new SftpClient(
            host: _senderConfig.Hostname,
            port: _senderConfig.Port,
            username: _senderConfig.Username,
            password: _senderConfig.Password);
    }

    public SerializationType? GetSerializationType() => null;

    public DetailedData<object> Send(Data<object> dataToSend)
    {
        _producer.WriteAllBytes(GetCurrentFilePath(dataToSend), dataToSend.CastObjectData<byte[]>().Body!); // Assumes data is byte[]
        return dataToSend.CloneDetailed();
    }

    /// <summary>
    /// Creates the full file path for the current file we want to send
    /// </summary>
    /// <param name="data"></param>
    /// <returns> The file path for the current file </returns>
    private string GetCurrentFilePath(Data<object> data) => Path.Combine(_senderConfig.Path,
        data.MetaData?.Storage?.Key ?? Generator.GenerateObjectName());


    public void Connect()
    {
        _producer.Connect();
    }

    public void Disconnect()
    {
        _producer.Disconnect();
    }
}