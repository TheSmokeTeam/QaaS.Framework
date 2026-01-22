using QaaS.Framework.SDK.Extensions;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Filters;
using Serilog.Sinks.Elasticsearch;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace QaaS.Framework.Executions;

public static class Constants
{
    public static readonly Serilog.ILogger DefaultSerilogLogger = new LoggerConfiguration()
        .MinimumLevel
        .Is(LogEventLevel.Verbose).WriteTo
        .Console(LogEventLevel.Information).AddQaaSElasticSink().CreateLogger();

    public static readonly ILogger DefaultLogger =
        new SerilogLoggerFactory(DefaultSerilogLogger).CreateLogger("DefaultLogger");

    // sends only logs that contains metadata to elasticSearch
    public static LoggerConfiguration AddQaaSElasticSink(this LoggerConfiguration configuration) =>
        configuration.WriteTo.Logger(logger => logger.WriteTo.Elasticsearch(GetElasticSearchSinkOptions())
            .MinimumLevel.Verbose()
            .Filter.ByIncludingOnly(Matching.WithProperty("Team")))
            .Enrich.WithHostname()
            .Enrich.WithEnvironment();
    
    private static ElasticsearchSinkOptions GetElasticSearchSinkOptions()
    {
        return new ElasticsearchSinkOptions(new Uri("REDA"))
        {
            IndexFormat = "qaas-{0:yyyy.MM.dd}",
            TemplateName = "qaas",
            AutoRegisterTemplate = true,
            AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
            OverwriteTemplate = true,
            TypeName = null,
            ModifyConnectionSettings = client => client.BasicAuthentication("REDA", "REDA")
                .ServerCertificateValidationCallback((connection, certificate, chain, errors) => true
                ).EnableHttpCompression().EnableDebugMode()
        };
    }
}