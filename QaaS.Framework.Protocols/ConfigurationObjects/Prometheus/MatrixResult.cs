namespace QaaS.Framework.Protocols.ConfigurationObjects.Prometheus;

public record MatrixResult
{
    public string Status { get; init; }
    public MatrixData Data { get; init; }
}

public record MatrixData
{
    public string ResultType { get; init; }
    public IList<MetricStruct> Result { get; init; }
}

public record MetricStruct
{
    public IDictionary<string, string> Metric { get; init; }
    public IList<IList<object>> Values { get; init; }
};
