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
    /// Builder constructor starts builder with an IConfigurationBuilder loaded with the given configurationFile
    /// </summary>
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
    /// Builder constructor starts builder with given IConfigurationBuilder
    /// </summary>
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

    /// <inheritdoc />
    public IContextBuilder SetLogger(ILogger logger)
    {
        _logger = logger;
        return this;
    }

    /// <inheritdoc />
    public IContextBuilder SetConfigurationFile(string? configurationFile)
    {
        if (configurationFile == null) return this;
        _configurationFile = configurationFile;
        return this;
    }

    /// <inheritdoc />
    public IContextBuilder WithOverwriteFile(string? overwriteFile)
    {
        if (overwriteFile == null) return this;
        _configurationOverwriteFiles.Add(overwriteFile);
        return this;
    }

    /// <inheritdoc />
    public IContextBuilder WithOverwriteFolder(string? overwriteFolder)
    {
        if (overwriteFolder == null) return this;
        _configurationOverwriteFolders.Add(overwriteFolder);
        return this;
    }

    /// <inheritdoc />
    public IContextBuilder SetCase(string? caseFile)
    {
        if (caseFile == null) return this;

        _caseName = caseFile;
        _caseFile = caseFile;
        return this;
    }

    /// <inheritdoc />
    public IContextBuilder SetExecutionId(string? executionId)
    {
        _executionId = executionId;
        return this;
    }

    /// <inheritdoc />
    public IContextBuilder WithOverwriteArgument(string? argument)
    {
        if (argument == null) return this;
        _configurationOverwriteArguments.Add(argument);
        return this;
    }

    /// <inheritdoc />
    public IContextBuilder WithReferenceResolution(ReferenceConfig referenceConfig)
    {
        _referenceConfigs.Add(referenceConfig);
        return this;
    }

    /// <inheritdoc />
    public IContextBuilder ResolveCaseLast()
    {
        _resolveCaseLast = true;
        return this;
    }

    /// <inheritdoc />
    public IContextBuilder WithEnvironmentVariableResolution()
    {
        _resolveWithEnvironmentVariables = true;
        return this;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public InternalContext BuildInternal()
        => new()
        {
            CaseName = _caseName,
            ExecutionId = _executionId,
            RootConfiguration = GetConfiguration(),
            InternalRunningSessions = _currentRunningSessions,
            Logger = _logger
        };


    /// <inheritdoc />
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

