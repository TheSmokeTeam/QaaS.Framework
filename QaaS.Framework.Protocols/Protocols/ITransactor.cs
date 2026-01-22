using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.Protocols.Protocols;

public interface ITransactor
{
    public SerializationType? GetInputCommunicationSerializationType();

    public SerializationType? GetOutputCommunicationSerializationType();

    public Tuple<DetailedData<object>, DetailedData<object>?> Transact(Data<object> dataToSend);
}