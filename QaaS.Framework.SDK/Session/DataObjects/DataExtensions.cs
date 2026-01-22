namespace QaaS.Framework.SDK.Session.DataObjects;

public static class DataExtensions
{
    public static DetailedData<T> CloneDetailed<T>(this Data<T> data, DateTime? datetime = null)
    {
        return new DetailedData<T>
        {
            Timestamp = datetime ?? DateTime.UtcNow,
            Body = data.Body,
            MetaData = data.MetaData
        };
    }
}