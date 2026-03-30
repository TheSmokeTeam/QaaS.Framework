using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.ExecutionObjects;

namespace QaaS.Framework.SDK.ContextObjects;

public abstract class BaseContext<TExecutionData> where TExecutionData : class, IExecutionData, new()
{
    private IConfiguration _rootConfiguration = CreateMutableConfiguration();
    private VariablesConfiguration _variables = new(CreateMutableConfiguration());
    // GlobalDict is shared mutable state across execution components, so nested reads/writes
    // must be serialized to avoid races while creating or traversing intermediate dictionaries.
    private readonly Lock _globalDictLock = new();

    /// <summary>
    /// Logger loaded from given QaaS configuration
    /// </summary>
    public ILogger Logger { get; init; } = null!;

    /// <summary>
    /// The data stored to manage the QaaS executions flows
    /// </summary>
    public TExecutionData ExecutionData { get; init; } = new();

    /// <summary>
    /// The root of the configuration given in the QaaS execution
    /// </summary>
    public IConfiguration RootConfiguration
    {
        get => _rootConfiguration;
        init => SetMutableRootConfiguration(value);
    }

    /// <summary>
    /// The nested variable accessor loaded from the root <c>variables</c> section.
    /// </summary>
    public dynamic Variables => _variables;

    /// <summary>
    /// Global dictionary that can be used throughout all QaaS` executions
    /// </summary>
    protected Dictionary<string, object?> GlobalDict { get; set; } = new();
    
    /// <summary>
    /// Gets the <see cref="GlobalDict"/> object
    /// </summary>
    public Dictionary<string, object?> GetGlobalDict => GlobalDict;
    /// <summary>
    /// Updates GlobalDict with new value at the requested path.
    /// If there are parts of the path that don't exist, they will be created.
    /// </summary>
    /// <param name="path">List of strings representing the path in the nested dictionary</param>
    /// <param name="value">Value to insert into the dictionary</param>
    public void InsertValueIntoGlobalDictionary(List<string> path, object? value)
    {
        if (path.Count == 0)
            throw new ArgumentException("GlobalDict path cannot be empty", nameof(path));
        lock (_globalDictLock)
        {
            UpdateDictRecursively(GlobalDict, path, value);
        }
    }

    /// <summary>
    /// Returns value from GlobalDict at a specific path
    /// </summary>
    /// <param name="path">List of strings representing the path in the nested dictionary</param>
    public object? GetValueFromGlobalDictionary(List<string> path)
    {
        if (path.Count == 0)
            throw new ArgumentException("GlobalDict path cannot be empty", nameof(path));

        try
        {
            lock (_globalDictLock)
            {
                return GetValueFromDictRecursively(GlobalDict, path);
            }
        }
        catch (KeyNotFoundException ex)
        {
            throw new KeyNotFoundException($"Path '{string.Join(".", path)}' does not exist in GlobalDict.", ex);
        }
    }
    
    /// <summary>
    /// Updates the dictionary recursively
    /// </summary>
    private void UpdateDictRecursively(Dictionary<string, object?> dict, List<string> path, object? value)
    {
        var key = path.First();
        if (path.Count == 1)
        {
            dict[path.First()] = value;
            return;
        }

        if (!dict.ContainsKey(key) || dict[key] is not Dictionary<string, object?>)
            dict[key] = new Dictionary<string, object?>();

        UpdateDictRecursively((Dictionary<string, object?>)dict[key]!, path.Skip(1).ToList(), value);
    }

    /// <summary>
    /// Retrieves value from nested dictionary
    /// </summary>
    private object? GetValueFromDictRecursively(Dictionary<string, object?> dict, List<string> path)
    {
        var key = path.First();
        if (path.Count == 1)
        {
            if (!dict.TryGetValue(key, out var value))
                throw new KeyNotFoundException($"Key '{key}' not found.");
            return value;
        }

        if (!dict.TryGetValue(key, out var nextDict) || nextDict is not Dictionary<string, object?> subDict)
            throw new KeyNotFoundException($"Intermediate key '{key}' not found or is not a dictionary.");

        return GetValueFromDictRecursively(subDict, path.Skip(1).ToList());
    }


    internal void SetRootConfiguration(IConfiguration updatedConfiguration) =>
        SetMutableRootConfiguration(updatedConfiguration);

    private void SetMutableRootConfiguration(IConfiguration configuration)
    {
        _rootConfiguration = CreateMutableConfiguration(configuration);
        _variables = ResolveVariablesConfiguration(_rootConfiguration);
    }

    private static VariablesConfiguration ResolveVariablesConfiguration(IConfiguration configuration) =>
        new(configuration);

    private static IConfiguration CreateMutableConfiguration(IConfiguration? configuration = null)
    {
        var builder = new ConfigurationBuilder();
        if (configuration != null)
            builder.AddInMemoryCollection(configuration.AsEnumerable());
        else
            builder.AddInMemoryCollection();

        return builder.Build();
    }
}
