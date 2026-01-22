using System.Net;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Configuration;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace QaaS.Framework.SDK.Extensions;

public static class SerilogExtensions
{
    /// <summary>
    /// Enriches log events with the machine hostname as a structured property and adds it as a tag (label) in Elasticsearch.
    /// </summary>
    /// <param name="enrichmentConfiguration">The <see cref="LoggerEnrichmentConfiguration"/> to enrich.</param>
    /// <returns>The enriched <see cref="LoggerEnrichmentConfiguration"/>.</returns>
    public static LoggerConfiguration WithHostname(this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        return enrichmentConfiguration
            .WithProperty("Hostname", Dns.GetHostName());
    }

    /// <summary>
    /// Enriches log events with the CI/Local environment as a structured property and adds it as a tag (label) in Elasticsearch.
    /// </summary>
    /// <param name="enrichmentConfiguration">The <see cref="LoggerEnrichmentConfiguration"/> to enrich.</param>
    /// <returns>The enriched <see cref="LoggerEnrichmentConfiguration"/>.</returns>
    public static LoggerConfiguration WithEnvironment(this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        return enrichmentConfiguration
            .WithProperty("Environment",
                Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") != null ? "CI" : "Local");
    }


    /// <summary>
    /// Extension Methods for ILogger
    /// </summary>
    /// <param name="logger"></param>
    extension(ILogger logger)
    {
        /// <summary>
        /// Logs information log with added metadata object
        /// </summary>
        public void LogInformationWithMetaData(string message, object metadata,
            params object?[] propertyValues)
        {
            var metaDataDict = metadata.GetType().GetProperties().Where(property => property.CanRead)
                .ToDictionary(property => property.Name, property => property.GetValue(metadata));
            using (logger.BeginScope(metaDataDict))
            {
                logger.LogInformation(message, propertyValues);
            }
        }

        /// <summary>
        /// Logs Warning log with added metadata object
        /// </summary>
        public void LogWarningWithMetaData(string message, object metadata,
            params object?[] propertyValues)
        {
            var metaDataDict = metadata.GetType().GetProperties().Where(property => property.CanRead)
                .ToDictionary(property => property.Name, property => property.GetValue(metadata));
            using (logger.BeginScope(metaDataDict))
            {
                logger.LogWarning(message, propertyValues);
            }
        }

        /// <summary>
        /// Logs Error log with added metadata object
        /// </summary>
        public void LogErrorWithMetaData(string message, object metadata,
            params object?[] propertyValues)
        {
            var metaDataDict = metadata.GetType().GetProperties().Where(property => property.CanRead)
                .ToDictionary(property => property.Name, property => property.GetValue(metadata));
            using (logger.BeginScope(metaDataDict))
            {
                logger.LogError(message, propertyValues);
            }
        }

        /// <summary>
        /// Logs Critical log with added metadata object
        /// </summary>
        public void LogCriticalWithMetaData(string message, object metadata,
            params object?[] propertyValues)
        {
            var metaDataDict = metadata.GetType().GetProperties().Where(property => property.CanRead)
                .ToDictionary(property => property.Name, property => property.GetValue(metadata));
            using (logger.BeginScope(metaDataDict))
            {
                logger.LogCritical(message, propertyValues);
            }
        }

        /// <summary>
        /// Logs Debug log with added metadata object
        /// </summary>
        public void LogDebugWithMetaData(string message, object metadata,
            params object?[] propertyValues)
        {
            var metaDataDict = metadata.GetType().GetProperties().Where(property => property.CanRead)
                .ToDictionary(property => property.Name, property => property.GetValue(metadata));
            using (logger.BeginScope(metaDataDict))
            {
                logger.LogDebug(message, propertyValues);
            }
        }

        /// <summary>
        /// Logs Trace log with added metadata object
        /// </summary>
        public void LogTraceWithMetaData(string message, object metadata,
            params object?[] propertyValues)
        {
            var metaDataDict = metadata.GetType().GetProperties().Where(property => property.CanRead)
                .ToDictionary(property => property.Name, property => property.GetValue(metadata));
            using (logger.BeginScope(metaDataDict))
            {
                logger.LogTrace(message, propertyValues);
            }
        }
    }
}