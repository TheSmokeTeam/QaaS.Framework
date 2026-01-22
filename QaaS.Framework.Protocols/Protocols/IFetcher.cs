using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Framework.Protocols.Protocols;

public interface IFetcher
{
    public IEnumerable<DetailedData<object>> Collect(DateTime sessionStartTimeUtc, DateTime sessionEndTimeUtc);
    public SerializationType? GetSerializationType();

}