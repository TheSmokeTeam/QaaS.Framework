using Microsoft.Extensions.Configuration;

namespace QaaS.Framework.SDK.ContextObjects;

/// <summary>
/// Provides nested access to the root <c>variables</c> configuration section.
/// </summary>
public sealed class VariablesConfiguration
{
    private readonly IConfiguration _configuration;
    private readonly string _path;

    internal VariablesConfiguration(IConfiguration configuration, string path = "variables")
    {
        _configuration = configuration;
        _path = path;
    }

    /// <summary>
    /// Gets a nested variable value or subsection relative to the current path.
    /// </summary>
    public dynamic this[string key]
    {
        get
        {
            var path = BuildPath(key);
            var value = _configuration[path];
            if (value is not null)
                return value;

            return new VariablesConfiguration(_configuration, path);
        }
        set => _configuration[BuildPath(key)] = value?.ToString();
    }

    /// <summary>
    /// Gets child configuration sections of the current variables node.
    /// </summary>
    public IEnumerable<IConfigurationSection> GetChildren() => _configuration.GetSection(_path).GetChildren();

    /// <summary>
    /// Gets the scalar value stored at the current variables node.
    /// </summary>
    public string? Value => _configuration[_path];

    public override string ToString() => Value ?? string.Empty;

    public static implicit operator string?(VariablesConfiguration configuration) => configuration.Value;

    private string BuildPath(string key) => string.IsNullOrWhiteSpace(_path)
        ? key
        : $"{_path}:{key}";
}
