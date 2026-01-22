using System.Collections;
using IBM.WMQ;
using QaaS.Framework.Protocols.ConfigurationObjects.IbmMq;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.Protocols.Protocols;

/// <summary>
/// Is a serialized reader that reads from ibm mq using the IBM.WMQ package.
/// </summary>
public class IbmMqProtocol : IReader
{
    private MQQueueManager? _manager;
    private MQQueue? _queue;
    private readonly Hashtable _properties;
    private readonly string? _managerName;
    private readonly string? _queueName;

    public IbmMqProtocol(IbmMqReaderConfig configuration)
    {
        _properties = new Hashtable()
        {
            { MQC.HOST_NAME_PROPERTY, configuration.HostName },
            { MQC.CHANNEL_PROPERTY, configuration.Channel },
            { MQC.PORT_PROPERTY, configuration.Port }
        };
        _managerName = configuration.Manager;
        _queueName = configuration.QueueName;
    }

    public SerializationType? GetSerializationType() => null;

    /// <inheritdoc />
    public DetailedData<object>? Read(TimeSpan timeout)
    {
        MQMessage message;
        try
        {
            message = GetMessage(timeout);
        }
        catch (MQException ex)
        {
            if (ex.Reason == MQC.MQRC_NO_MSG_AVAILABLE)
                return null;
            throw;
        }

        return new DetailedData<object> { Body = message.ReadBytes(message.DataLength) };
    }

    /// <summary>
    /// Constructs and reads message from the queue within given timeout.
    /// </summary>
    protected virtual MQMessage GetMessage(TimeSpan timeout)
    {
        var message = new MQMessage();
        var readOptions = new MQGetMessageOptions
        {
            Options = MQC.MQGMO_WAIT,
            WaitInterval = (int)timeout.TotalMilliseconds
        };
        _queue!.Get(message, readOptions);
        return message;
    }

    public void Connect()
    {
        _manager = new MQQueueManager(_managerName, _properties);
        _queue = _manager.AccessQueue(_queueName, MQC.MQOO_INPUT_AS_Q_DEF | MQC.MQOO_FAIL_IF_QUIESCING);
    }

    public void Disconnect()
    {
        _manager?.Disconnect();
        _queue?.Close();
    }

    /// <summary>
    /// Closes all open ibmmq connections.
    /// </summary>
    public void Dispose()
    {
        _manager?.Disconnect();
        _queue?.Close();
    }
}