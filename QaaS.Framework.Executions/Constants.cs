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
    public static readonly Serilog.ILogger DefaultSerilogLogger = BuildDefaultSerilogLogger();

    public static readonly ILogger DefaultLogger =
        new SerilogLoggerFactory(DefaultSerilogLogger).CreateLogger("DefaultLogger");

    private static Serilog.ILogger BuildDefaultSerilogLogger()
    {
        var warnings = new List<string>();
        var logger = new LoggerConfiguration()
            .MinimumLevel
            .Is(LogEventLevel.Verbose).WriteTo
            .Console(LogEventLevel.Information).AddQaaSElasticSink(warningLogger: warnings.Add).CreateLogger();
        foreach (var warning in warnings)
        {
            logger.Warning("{WarningMessage}", warning);
        }

        return logger;
    }

    // sends only logs that contains metadata to elasticSearch
    public static LoggerConfiguration AddQaaSElasticSink(
        this LoggerConfiguration configuration,
        string? elasticUri = null,
        string? username = null,
        string? password = null,
        Action<string>? warningLogger = null)
    {
        warningLogger ??= _ => { };

        if (string.IsNullOrWhiteSpace(elasticUri))
        {
            warningLogger("Elasticsearch logging is enabled, but no Elasticsearch URI was provided. Skipping Elasticsearch sink.");
            return configuration;
        }

        if (!Uri.TryCreate(elasticUri, UriKind.Absolute, out var parsedElasticUri))
        {
            warningLogger(
                $"Elasticsearch logging is enabled, but URI '{elasticUri}' is invalid. Skipping Elasticsearch sink.");
            return configuration;
        }

        return configuration.WriteTo.Logger(logger => logger.WriteTo.Elasticsearch(
                GetElasticSearchSinkOptions(parsedElasticUri, username, password, warningLogger))
            .MinimumLevel.Verbose()
            .Filter.ByIncludingOnly(Matching.WithProperty("Team")))
            .Enrich.WithHostname()
            .Enrich.WithEnvironment();
    }

    private static ElasticsearchSinkOptions GetElasticSearchSinkOptions(
        Uri elasticUri,
        string? username,
        string? password,
        Action<string> warningLogger)
    {
        var hasUsername = !string.IsNullOrWhiteSpace(username);
        var hasPassword = !string.IsNullOrWhiteSpace(password);
        var useBasicAuthentication = hasUsername && hasPassword;

        if (hasUsername != hasPassword)
        {
            warningLogger(
                "Only one Elasticsearch credential was provided. Continuing without basic authentication.");
        }
        else if (!useBasicAuthentication)
        {
            warningLogger(
                "Elasticsearch username/password were not provided. Continuing without basic authentication.");
        }

        return new ElasticsearchSinkOptions(elasticUri)
        {
            IndexFormat = "qaas-{0:yyyy.MM.dd}",
            TemplateName = "qaas",
            AutoRegisterTemplate = true,
            AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
            OverwriteTemplate = true,
            TypeName = null,
            ModifyConnectionSettings = client =>
            {
                var configuredClient = useBasicAuthentication
                    ? client.BasicAuthentication(username!, password!)
                    : client;

                return configuredClient
                    .ServerCertificateValidationCallback((connection, certificate, chain, errors) => true)
                    .EnableHttpCompression()
                    .EnableDebugMode();
            }
        };
    }
}
