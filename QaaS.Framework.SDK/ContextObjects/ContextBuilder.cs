using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QaaS.Framework.Configurations;
using QaaS.Framework.Configurations.ConfigurationBuilderExtensions;
using QaaS.Framework.Configurations.References;
using QaaS.Framework.SDK.Session.SessionDataObjects.RunningSessionsObjects;

namespace QaaS.Framework.SDK.ContextObjects;

/// <inheritdoc />
public class ContextBuilder : IContextBuilder
{
    private readonly List<string> _configurationOverwriteFiles = new();
    private readonly List<string> _configurationOverwriteFolders = new();
    private readonly List<string> _configurationOverwriteArguments = new();
    private readonly List<ReferenceConfig> _referenceConfigs = new();
    private readonly IConfigurationBuilder _configurationBuilder;
    private readonly IList<string>? _referenceResolutionPaths;
    private readonly IList<string>? _uniqueIdPathRegexes;
    private IInternalRunningSessions _currentRunningSessions =
        new RunningSessions(
            new Dictionary<string, QaaS.Framework.SDK.Session.SessionDataObjects.RunningSessionData<object, object>>());
    private ILogger _logger = NullLogger.Instance;
    private bool _resolveCaseLast = false;
    private bool _resolveWithEnvironmentVariables = false;
    private string? _configurationFile;
    private string? _caseFile;
    private string? _caseName;
    private string? _executionId;

    /// <summary>
    /// Creates a context builder that starts from a base QaaS configuration file.
    /// </summary>
    /// <remarks>
    /// Use this constructor when the context should load its initial configuration from a file path before overwrite sources and reference resolution are applied.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Contexts" />
    /// <param name="configurationFile"> The relative/full path to the base `.qaas.yaml` configuration file </param>
    /// <param name="referenceResolutionPaths"> Used in
    /// <see cref="ConfigurationReferencesParser.ResolveReferencesInConfiguration"/> as parameter of the same name </param>
    /// <param name="uniqueIdPathRegexes"> Used in
    /// <see cref="ConfigurationReferencesParser.ResolveReferencesInConfiguration"/> as parameter of the same name </param>
    public ContextBuilder(string configurationFile,
        IList<string>? referenceResolutionPaths = null,
        IList<string>? uniqueIdPathRegexes = null)
    {
        _configurationBuilder = new ConfigurationBuilder();
        _configurationFile = configurationFile;
        _referenceResolutionPaths = referenceResolutionPaths;
        _uniqueIdPathRegexes = uniqueIdPathRegexes;
    }

    /// <summary>
    /// Creates a context builder that starts from an existing configuration builder pipeline.
    /// </summary>
    /// <remarks>
    /// Use this constructor when configuration sources are assembled externally and should be handed to the QaaS context pipeline as-is.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Contexts" />
    /// <param name="configurationBuilder"> A configuration builder to build the context's configurations with </param>
    /// <param name="referenceResolutionPaths"> Used in
    /// <see cref="ConfigurationReferencesParser.ResolveReferencesInConfiguration"/> as parameter of the same name </param>
    /// <param name="uniqueIdPathRegexes"> Used in
    /// <see cref="ConfigurationReferencesParser.ResolveReferencesInConfiguration"/> as parameter of the same name </param>
    public ContextBuilder(IConfigurationBuilder configurationBuilder,
        IList<string>? referenceResolutionPaths = null,
        IList<string>? uniqueIdPathRegexes = null)
    {
        _configurationBuilder = configurationBuilder;
        _referenceResolutionPaths = referenceResolutionPaths;
        _uniqueIdPathRegexes = uniqueIdPathRegexes;
    }

    /// <summary>
    /// Sets the logger stored on the built context.
    /// </summary>
    /// <remarks>
    /// The configured logger becomes the logger used by the context itself and by runtime components resolved from that context.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Contexts" />
    public IContextBuilder SetLogger(ILogger logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// Sets the base configuration file used by the context builder.
    /// </summary>
    /// <remarks>
    /// Use this when the base configuration file should be selected or replaced after the builder has been created.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Contexts" />
    public IContextBuilder SetConfigurationFile(string? configurationFile)
    {
        if (configurationFile == null) return this;
        _configurationFile = configurationFile;
        return this;
    }

    /// <summary>
    /// Adds an overwrite file that should be applied during context construction.
    /// </summary>
    /// <remarks>
    /// Overwrite files are applied after the base configuration and before the final configuration is built.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Contexts" />
    public IContextBuilder WithOverwriteFile(string? overwriteFile)
    {
        if (overwriteFile == null) return this;
        _configurationOverwriteFiles.Add(overwriteFile);
        return this;
    }

    /// <summary>
    /// Adds an overwrite folder whose YAML files should be applied during context construction.
    /// </summary>
    /// <remarks>
    /// Every YAML file discovered in the folder is applied as an overwrite source in the order returned by the file-system enumeration.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Contexts" />
    public IContextBuilder WithOverwriteFolder(string? overwriteFolder)
    {
        if (overwriteFolder == null) return this;
        _configurationOverwriteFolders.Add(overwriteFolder);
        return this;
    }

    /// <summary>
    /// Sets the case file used during context construction.
    /// </summary>
    /// <remarks>
    /// The supplied value is also stored as the case name on the built context.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Contexts" />
    public IContextBuilder SetCase(string? caseFile)
    {
        if (caseFile == null) return this;

        _caseName = caseFile;
        _caseFile = caseFile;
        return this;
    }

    /// <summary>
    /// Sets the execution identifier stored on the built context.
    /// </summary>
    /// <remarks>
    /// The execution identifier flows into the built context and can later be used by logging, reports, and storage integrations.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Contexts" />
    public IContextBuilder SetExecutionId(string? executionId)
    {
        _executionId = executionId;
        return this;
    }

    /// <summary>
    /// Adds a command-line style overwrite argument to the context builder.
    /// </summary>
    /// <remarks>
    /// Use this when command-line style overrides should participate in the same configuration pipeline as YAML sources.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Contexts" />
    public IContextBuilder WithOverwriteArgument(string? argument)
    {
        if (argument == null) return this;
        _configurationOverwriteArguments.Add(argument);
        return this;
    }

    /// <summary>
    /// Adds a reference-resolution rule to the context builder.
    /// </summary>
    /// <remarks>
    /// Reference-resolution rules are applied while building the final configuration so linked configuration values can be expanded consistently.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Contexts" />
    public IContextBuilder WithReferenceResolution(ReferenceConfig referenceConfig)
    {
        _referenceConfigs.Add(referenceConfig);
        return this;
    }

    /// <summary>
    /// Delays case-file application until after reference resolution has completed.
    /// </summary>
    /// <remarks>
    /// This changes resolution order so the case overlay is applied after references are expanded from the base configuration and overwrites.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Contexts" />
    public IContextBuilder ResolveCaseLast()
    {
        _resolveCaseLast = true;
        return this;
    }

    /// <summary>
    /// Enables environment-variable expansion while the context is being built.
    /// </summary>
    /// <remarks>
    /// Enable this when configuration values should resolve environment variables while the context is being built.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Contexts" />
    public IContextBuilder WithEnvironmentVariableResolution()
    {
        _resolveWithEnvironmentVariables = true;
        return this;
    }

    /// <summary>
    /// Sets the running-session store used by the built context.
    /// </summary>
    /// <remarks>
    /// The running-session store allows runtime components to coordinate and inspect active sessions through the built context.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Contexts" />
    public IContextBuilder SetCurrentRunningSessions(IInternalRunningSessions runningSessions)
    {
        _currentRunningSessions = runningSessions;
        return this;
    }

    private IConfiguration GetConfiguration()
    {
        // Base configuration .qaas.yaml file
        if (_configurationFile != null) _configurationBuilder.AddYaml(_configurationFile);
        // Overwriting variable .yaml files
        foreach (var overwriteFile in _configurationOverwriteFiles) _configurationBuilder.AddYaml(overwriteFile);
        foreach (var overwriteFolder in _configurationOverwriteFolders)
        foreach (var overwriteFile in PathUtils.EnumerateYamlFilesInDirectory(overwriteFolder))
            _configurationBuilder.AddYaml(overwriteFile);

        IConfiguration? configuration;
        if (!_resolveCaseLast)
        {
            // Case .yaml file overwrite
            if (_caseFile != null) _configurationBuilder.AddYaml(_caseFile);
            // Build configuration and then resolve references
            configuration = new ConfigurationBuilder().AddConfiguration(_configurationBuilder
                .AddCommandLine(_configurationOverwriteArguments.ToArray()).Build()
                .ResolveReferencesInConfiguration(_referenceConfigs, _referenceResolutionPaths, _uniqueIdPathRegexes,
                    _resolveWithEnvironmentVariables)).EnrichedBuild(_resolveWithEnvironmentVariables);
        }
        else
        {
            var tmpConfigurationBuilder = new ConfigurationBuilder().AddConfiguration(_configurationBuilder
                .AddCommandLine(_configurationOverwriteArguments.ToArray()).Build()
                .ResolveReferencesInConfiguration(_referenceConfigs, _referenceResolutionPaths, _uniqueIdPathRegexes,
                    _resolveWithEnvironmentVariables));
            // Case .yaml file overwrite
            if (_caseFile != null) tmpConfigurationBuilder.AddYaml(_caseFile);
            configuration = tmpConfigurationBuilder.EnrichedBuild(_resolveWithEnvironmentVariables);
        }

        return configuration;
    }

    /// <summary>
    /// Builds an internal QaaS context from the current builder state.
    /// </summary>
    /// <remarks>
    /// Call this after all configuration inputs, overwrite sources, and resolution options have been registered on the builder. The returned internal context is used by the runtime bootstrap flow.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Contexts" />
    public InternalContext BuildInternal()
        => new()
        {
            CaseName = _caseName,
            ExecutionId = _executionId,
            RootConfiguration = GetConfiguration(),
            InternalRunningSessions = _currentRunningSessions,
            Logger = _logger
        };


    /// <summary>
    /// Builds the obsolete public Context projection from the current builder state.
    /// </summary>
    /// <remarks>
    /// Prefer BuildInternal() for the active runtime path.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Contexts" />
    [Obsolete("Function no longer in use, Use BuildInternal instead")]
    public Context Build()
        => new()
        {
            CaseName = _caseName,
            ExecutionId = _executionId,
            RootConfiguration = GetConfiguration(),
            CurrentRunningSessions = _currentRunningSessions,
            Logger = _logger
        };
}

