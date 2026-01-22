using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.Protocols.Protocols;

public interface IChunkReader : IConnectable
{
    public SerializationType? GetSerializationType();

    public IEnumerable<DetailedData<object>> ReadChunk(TimeSpan timeout);
}