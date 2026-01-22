using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.Protocols.Protocols;

public interface IChunkSender : IConnectable
{
    public IEnumerable<DetailedData<object>> SendChunk(IEnumerable<Data<object>> chunkDataToSend);
    public SerializationType? GetSerializationType();

}