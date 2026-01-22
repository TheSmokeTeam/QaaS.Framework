using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.Protocols.Protocols;

public interface ISender : IConnectable
{
    public SerializationType? GetSerializationType();

    public DetailedData<object> Send(Data<object> dataToSend);
}