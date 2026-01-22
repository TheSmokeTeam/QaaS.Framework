using Microsoft.Extensions.Logging;
using QaaS.Framework.Protocols.ConfigurationObjects;
using QaaS.Framework.Protocols.ConfigurationObjects.Grpc;
using QaaS.Framework.Protocols.ConfigurationObjects.Http;
namespace QaaS.Framework.Protocols.Protocols.Factories;

/// <summary>
/// Factory class responsible for creating transactor instances based on configuration types.
/// Provides methods to create transactor instances for different communication protocols.
/// </summary>
public static class TransactorFactory
{
    /// <summary>
    /// Creates a transactor instance based on the provided configuration type.
    /// </summary>
    /// <param name="type">The transactor configuration that determines the type of transactor to create</param>
    /// <param name="logger">Logger instance for logging operations and errors</param>
    /// <param name="timeOut">Timeout duration for the transactor operations</param>
    /// <returns>An instance of ITransactor configured according to the provided configuration</returns>
    /// <exception cref="InvalidOperationException">Thrown when the provided configuration type is not supported or recognized</exception>
    public static ITransactor CreateTransactor(ITransactorConfig type, ILogger logger, TimeSpan timeOut)
    {
        return type switch
        {
            HttpTransactorConfig config => new HttpProtocol(config, logger, timeOut),
            GrpcTransactorConfig config => new GrpcProtocol(config, logger, timeOut),
            _ => throw new InvalidOperationException($"Protocol type {type.GetType().Name} is not supported")
        };
    }
}