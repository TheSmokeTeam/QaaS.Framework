using Microsoft.Extensions.Logging;
using QaaS.Framework.Configurations.References;
using QaaS.Framework.SDK.Session.SessionDataObjects.RunningSessionsObjects;

namespace QaaS.Framework.SDK.ContextObjects;

/// <summary>
/// Builds a context object from configuration files
/// </summary>
public interface IContextBuilder
{
    /// <summary>
    /// Sets the context's logger
    /// </summary>
    /// <param name="logger"> The logger to set the context with </param>
    /// <returns> he current builder </returns>
    public IContextBuilder SetLogger(ILogger logger);

    /// <summary>
    /// Sets the root .qaas.yaml configuration file
    /// </summary>
    /// <param name="configurationFile"> The relative/full path to the configuration `.qaas.yaml` file </param>
    /// <returns> The current builder </returns>
    public IContextBuilder SetConfigurationFile(string? configurationFile);

    /// <summary>
    /// Adds an overwriting file that overwrites the previously given yaml files
    /// </summary>
    /// <param name="overwriteFile"> The relative/full path to the overwriting `.yaml` file </param>
    /// <returns> The current builder </returns>
    public IContextBuilder WithOverwriteFile(string? overwriteFile);

    /// <summary>
    /// Sets the case the context belongs to (If not set the context does not belong to any case)
    /// </summary>
    /// <param name="caseFile"> The relative path to the .yaml case file </param>
    /// <returns> The current builder </returns>
    public IContextBuilder SetCase(string? caseFile);

    /// <summary>
    /// Sets the execution Id the context runs in (If not set the context is created under a run with only 1 execution)
    /// </summary>
    /// <param name="executionId"> The id of the execution </param>
    /// <returns> the current builder </returns>
    public IContextBuilder SetExecutionId(string? executionId);

    /// <summary>
    /// Adds an argument to overwrite the configuration with.
    /// the argument should be structured as follows - Path:To:Variable:To:Overwrite=NewVariableValue
    /// </summary>
    /// <param name="argument"> The argument to overwrite the configuration with </param>
    /// <returns> The current builder </returns>
    public IContextBuilder WithOverwriteArgument(string? argument);

    /// <summary>
    /// Adds a reference resolution for the configurations loaded to context
    /// </summary>
    /// <param name="referenceConfig"> The configurations for how to resolve the given reference </param>
    /// <returns> The current builder </returns>
    public IContextBuilder WithReferenceResolution(ReferenceConfig referenceConfig);

    /// <summary>
    /// Sets the builder to resolve cases last, after all other configuration resolutions.
    /// </summary>
    /// <returns> The current builder </returns>
    public IContextBuilder ResolveCaseLast();

    /// <summary>
    /// Adds environment variable resolution for the configurations loaded to context
    /// </summary>
    /// <returns> The current builder </returns>
    public IContextBuilder WithEnvironmentVariableResolution();

    /// <summary>
    /// Sets the current running sessions for the configurations loaded to context
    /// </summary>
    /// <returns> The current builder </returns>
    public IContextBuilder SetCurrentRunningSessions(IInternalRunningSessions runningSessions);

    /// <summary>
    /// Builds the <see cref="Context"/> with the configured parameters
    /// </summary>
    /// <returns> The built <see cref="Context"/> </returns>
    public Context Build();
    
    /// <summary>
    /// Builds the <see cref="InternalContext"/> with the configured parameters
    /// </summary>
    /// <returns> The built <see cref="InternalContext"/> </returns>
    public InternalContext BuildInternal();
}