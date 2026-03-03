namespace QaaS.Framework.Protocols.ConfigurationObjects.Prometheus;

public record MatrixResult
{
    public string Status { get; init; } = string.Empty;
    public MatrixData Data { get; init; } = new();
}

public record MatrixData
{
    public string ResultType { get; init; } = string.Empty;
    public IList<MetricStruct> Result { get; init; } = [];
}

public record MetricStruct
{
    public IDictionary<string, string> Metric { get; init; } = new Dictionary<string, string>();
    public IList<IList<object>> Values { get; init; } = [];
};
