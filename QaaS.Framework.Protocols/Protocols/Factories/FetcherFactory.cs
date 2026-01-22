using QaaS.Framework.Protocols.ConfigurationObjects;
using QaaS.Framework.Protocols.ConfigurationObjects.Prometheus;
using Microsoft.Extensions.Logging;

namespace QaaS.Framework.Protocols.Protocols.Factories
{
    /// <summary>
    /// Factory class responsible for creating fetcher instances based on configuration types.
    /// Provides methods to create fetcher instances for different data sources.
    /// </summary>
    public static class FetcherFactory
    {
        /// <summary>
        /// Creates a fetcher instance based on the provided configuration type.
        /// </summary>
        /// <param name="type">The fetcher configuration that determines the type of fetcher to create</param>
        /// <param name="logger">Logger instance for logging operations and errors</param>
        /// <returns>An instance of IFetcher configured according to the provided configuration</returns>
        /// <exception cref="InvalidOperationException">Thrown when the provided configuration type is not supported or recognized</exception>
        public static IFetcher CreateFetcher(IFetcherConfig type, ILogger logger)
        {
            return type switch
            {
                PrometheusFetcherConfig config => new PrometheusProtocol(config, logger),
                _ => throw new InvalidOperationException($"Protocol type {type.GetType().Name} is not supported")
            };
        }
    }
}