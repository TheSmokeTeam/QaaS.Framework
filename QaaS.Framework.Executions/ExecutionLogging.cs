using QaaS.Framework.SDK.Extensions;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Filters;
using Serilog.Sinks.Elasticsearch;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace QaaS.Framework.Executions;

public sealed record ElasticLoggingDefaults
{
    public bool SendLogs { get; init; }

    public string? ElasticUri { get; init; }

    public string? ElasticUsername { get; init; }

    public string? ElasticPassword { get; init; }
}

public interface IElasticLoggingDefaultsProvider
{
    ElasticLoggingDefaults GetDefaults();
}

public static class ExecutionLogging
{
    private sealed class StaticElasticLoggingDefaultsProvider(ElasticLoggingDefaults defaults)
        : IElasticLoggingDefaultsProvider
    {
        public ElasticLoggingDefaults GetDefaults() => defaults;
    }

    public static readonly Serilog.ILogger DefaultSerilogLogger = BuildDefaultSerilogLogger();

    public static readonly ILogger DefaultLogger =
        new SerilogLoggerFactory(DefaultSerilogLogger).CreateLogger("DefaultLogger");

    private static IElasticLoggingDefaultsProvider? _defaultsProvider;

    public static void RegisterDefaultsProvider(IElasticLoggingDefaultsProvider defaultsProvider)
    {
        ArgumentNullException.ThrowIfNull(defaultsProvider);
        _defaultsProvider = defaultsProvider;
    }

    public static IElasticLoggingDefaultsProvider? GetDefaultsProvider() => _defaultsProvider;

    public static void RegisterDefaults(
        bool sendLogs,
        string? elasticUri = null,
        string? elasticUsername = null,
        string? elasticPassword = null) =>
        RegisterDefaultsProvider(new StaticElasticLoggingDefaultsProvider(new ElasticLoggingDefaults
        {
            SendLogs = sendLogs,
            ElasticUri = elasticUri,
            ElasticUsername = elasticUsername,
            ElasticPassword = elasticPassword
        }));

    internal static ResolvedElasticLoggingOptions ResolveElasticLoggingOptions(Options.LoggerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var defaults = ShouldApplyDefaultsProvider(options)
            ? GetDefaultsProvider()?.GetDefaults()
            : null;

        return new ResolvedElasticLoggingOptions(
            options.LoggerLevel,
            options.LoggerConfigurationFilePath,
            defaults?.SendLogs ?? options.SendLogs,
            defaults?.ElasticUri ?? options.ElasticUri,
            defaults?.ElasticUsername ?? options.ElasticUsername,
            defaults?.ElasticPassword ?? options.ElasticPassword);
    }

    internal static bool ShouldApplyDefaultsProvider(Options.LoggerOptions options) =>
        options.LoggerConfigurationFilePath is null &&
        !options.SendLogs &&
        string.IsNullOrWhiteSpace(options.ElasticUri) &&
        string.IsNullOrWhiteSpace(options.ElasticUsername) &&
        string.IsNullOrWhiteSpace(options.ElasticPassword);

    private static Serilog.ILogger BuildDefaultSerilogLogger()
    {
        // The framework-level fallback logger must stay console-only.
        // Elasticsearch shipping is configured per run through LoggerOptions.SendLogs, and the
        // default logger should not emit sink warnings before any command opts into that path.
        return new LoggerConfiguration()
            .MinimumLevel
            .Is(LogEventLevel.Verbose)
            .WriteTo
            .Console(LogEventLevel.Information)
            .CreateLogger();
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

internal sealed record ResolvedElasticLoggingOptions(
    LogEventLevel? LoggerLevel,
    string? LoggerConfigurationFilePath,
    bool SendLogs,
    string? ElasticUri,
    string? ElasticUsername,
    string? ElasticPassword);
