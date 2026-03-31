using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations;
using QaaS.Framework.Configurations.ConfigurationBindingUtils;
using QaaS.Framework.Configurations.CustomValidationAttributes;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Hooks.Generator;
using QaaS.Framework.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

[assembly: InternalsVisibleTo("QaaS.Framework.Executions")]
[assembly: InternalsVisibleTo("QaaS.Framework.SDK.Tests")]
[assembly: InternalsVisibleTo("QaaS.Runner")]

namespace QaaS.Framework.SDK.DataSourceObjects;

public class DataSourceBuilder : IYamlConvertible
{
    private DataSource _dataSource = null!;

    [Required, Description("Name of data source to reference it by (must be unique)")]
    public string? Name { get; internal set; }

    [Required, Description("The name of the generator to use")]
    public string? Generator { get; internal set; }
    [Description("True to iterate over data lazily"), DefaultValue(false)]
    public bool Lazy { get; internal set; } = false;
    [EnumerablePropertyDoesNotContainAnotherPropertyValue(nameof(Name)),
     Description("Names of data sources to pass to this data source for usage, those data sources dont have to be" +
                 " defined before this data source.")]
    public string[] DataSourceNames { get; internal set; } = [];
    [EnumerablePropertyDoesNotContainAnotherPropertyValue(nameof(Name)),
     Description(
         "Regex patterns of data sources to pass to this data source for usage, those data sources dont have to be" +
         " defined before this data source.")]
    public string[] DataSourcePatterns { get; internal set; } = [];
    [Description("Implementation configuration for the generator, " +
                 "the configuration given here is loaded into the provided generator dynamically.")]
    public IConfiguration GeneratorConfiguration { get; internal set; } = new ConfigurationBuilder().Build();
    public IConfiguration Configuration
    {
        get => GeneratorConfiguration;
        internal set => GeneratorConfiguration = value ?? new ConfigurationBuilder().Build();
    }
    [Description("Serialize to use on the generated data"), DefaultValue(null)]
    [NullUnlessAll(new[] { nameof(Deserialize) }, [null])]
    public SerializeConfig? Serialize { get; internal set; } = null;
    [Description("Deserialize to use on the generated data"), DefaultValue(null)]
    [NullUnlessAll(new[] { nameof(Serialize) }, [null])]
    public DeserializeConfig? Deserialize { get; internal set; } = null;
    /// <summary>
    /// Sets the name used for the current Framework data source builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework data source builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public DataSourceBuilder Named(string name)
    {
        Name = name;
        return this;
    }

    /// <summary>
    /// Sets the hook implementation name used by the current Framework data source builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework data source builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public DataSourceBuilder HookNamed(string hookName)
    {
        Generator = hookName;
        return this;
    }

    /// <summary>
    /// Creates or adds the configured data source name entry on the current Framework data source builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework data source builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public DataSourceBuilder CreateDataSourceName(string dataSourceName)
    {
        DataSourceNames = (DataSourceNames ?? []).Append(dataSourceName).ToArray();
        return this;
    }

    /// <summary>
    /// Returns the configured data source names currently stored on the Framework data source builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework data source builder API surface in code. Use it to inspect the current configured state without rebuilding the surrounding collection or runtime object graph.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public IReadOnlyList<string> ReadDataSourceNames()
    {
        return DataSourceNames ?? [];
    }

    /// <summary>
    /// Updates the configured data source name stored on the current Framework data source builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework data source builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public DataSourceBuilder UpdateDataSourceName(string existingValue, string newValue)
    {
        if (DataSourceNames == null)
            return this;

        var index = Array.IndexOf(DataSourceNames, existingValue);
        if (index >= 0)
            DataSourceNames[index] = newValue;

        return this;
    }

    /// <summary>
    /// Removes the configured data source name from the current Framework data source builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework data source builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public DataSourceBuilder DeleteDataSourceName(string dataSourceName)
    {
        DataSourceNames = (DataSourceNames ?? []).Where(value => value != dataSourceName).ToArray();
        return this;
    }

    /// <summary>
    /// Creates or adds the configured data source pattern entry on the current Framework data source builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework data source builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public DataSourceBuilder CreateDataSourcePattern(string dataSourcePattern)
    {
        DataSourcePatterns = (DataSourcePatterns ?? []).Append(dataSourcePattern).ToArray();
        return this;
    }

    /// <summary>
    /// Returns the configured data source patterns currently stored on the Framework data source builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework data source builder API surface in code. Use it to inspect the current configured state without rebuilding the surrounding collection or runtime object graph.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public IReadOnlyList<string> ReadDataSourcePatterns()
    {
        return DataSourcePatterns ?? [];
    }

    /// <summary>
    /// Updates the configured data source pattern stored on the current Framework data source builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework data source builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public DataSourceBuilder UpdateDataSourcePattern(string existingValue, string newValue)
    {
        if (DataSourcePatterns == null)
            return this;

        var index = Array.IndexOf(DataSourcePatterns, existingValue);
        if (index >= 0)
            DataSourcePatterns[index] = newValue;

        return this;
    }

    /// <summary>
    /// Removes the configured data source pattern from the current Framework data source builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework data source builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public DataSourceBuilder DeleteDataSourcePattern(string dataSourcePattern)
    {
        DataSourcePatterns = (DataSourcePatterns ?? []).Where(value => value != dataSourcePattern).ToArray();
        return this;
    }

    /// <summary>
    /// Sets the serializer configuration used by the current Framework data source builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework data source builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public DataSourceBuilder WithSerializer(SerializeConfig serializeConfig)
    {
        Serialize = serializeConfig;
        return this;
    }

    /// <summary>
    /// Sets the deserializer configuration used by the current Framework data source builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework data source builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public DataSourceBuilder WithDeserializer(DeserializeConfig deserializeConfig)
    {
        Deserialize = deserializeConfig;
        return this;
    }

    /// <summary>
    /// Marks the data source for lazy resolution.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework data source builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public DataSourceBuilder IsLazy()
    {
        Lazy = true;
        return this;
    }

    /// <summary>
    /// Replaces the generator configuration with the supplied object.
    /// </summary>
    /// <remarks>
    /// The supplied object is serialized to JSON and loaded into the builder as the new generator configuration.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public DataSourceBuilder Configure(object configuration)
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(configuration)));
        GeneratorConfiguration = new ConfigurationBuilder().AddJsonStream(stream).Build();
        return this;
    }

    /// <summary>
     /// Clears the current generator configuration.
    /// </summary>
    /// <remarks>
    /// After this call, the builder holds an empty configuration until a new one is supplied.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public DataSourceBuilder DeleteConfiguration()
    {
        GeneratorConfiguration = new ConfigurationBuilder().Build();
        return this;
    }

    /// <summary>
    /// Returns the configuration currently stored on the Framework data source builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Framework data source builder API surface in code. Use it to inspect the current configured state without rebuilding the surrounding collection or runtime object graph.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public DataSourceBuilder UpdateConfiguration(object configuration)
    {
        GeneratorConfiguration = GeneratorConfiguration.UpdateConfiguration(configuration);
        return this;
    }

    /// <summary>
    /// Rejects custom YAML deserialization for DataSourceBuilder.
    /// </summary>
    /// <remarks>
    /// DataSourceBuilder only supports YAML serialization through Write; custom deserialization through IYamlConvertible.Read is intentionally unsupported.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        throw new NotSupportedException($"{nameof(Read)} doesn't support custom" +
                                        $" deserialization from Yaml for {nameof(DataSourceBuilder)}");
    }

    /// <summary>
    /// Writes the current Framework data source builder configuration to the configured serializer output.
    /// </summary>
    /// <remarks>
    /// This method participates in the YAML serialization surface that backs configuration-as-code support.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public void Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
    {
        var generatorConfiguration = GeneratorConfiguration
            .GetDictionaryFromConfiguration();
        nestedObjectSerializer(new
        {
            Name,
            Generator,
            Lazy,
            DataSourceNames,
            DataSourcePatterns,
            Serialize,
            Deserialize,
            GeneratorConfiguration = generatorConfiguration
        });
    }

    /// <summary>
    /// Registers the configured data source definition and returns the resulting data source descriptor.
    /// </summary>
    /// <remarks>
    /// Registration produces the immutable data-source descriptor that is later resolved against generator hooks during execution build.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public DataSource Register()
    {
        _dataSource = new DataSource
        {
            Name = Name!,
            Deserializer = DeserializerFactory.BuildDeserializer(Deserialize?.Deserializer),
            DeserializerSpecificType = Deserialize?.SpecificType?.GetConfiguredType(),
            Serializer = SerializerFactory.BuildSerializer(Serialize?.Serializer),
            Lazy = Lazy,
        };
        return _dataSource;
    }


    /// <summary>
    /// Builds the configured data source for execution.
    /// </summary>
    /// <remarks>
    /// This resolves the configured generator, links any referenced data sources, and finalizes the registered data source before execution begins.
    /// </remarks>
    /// <qaas-docs group="Framework APIs" subgroup="Data Sources" />
    public DataSource Build(InternalContext context,IEnumerable<DataSource> dataSources,
        IEnumerable<KeyValuePair<string, IGenerator>> generators)
    {
        var generator = generators.FirstOrDefault(pair => pair.Key == Generator!).Value ??
                        throw new ArgumentException($"Data source {Name}'s provided generator {Generator} was" +
                                                    $" not found in provided generators.");
        context.Logger.LogDebugWithMetaData(
            "Started building Generator of type {type}",
            context.GetMetaDataFromContext(),
            new object?[] { Generator ?? string.Empty });
        var otherDataSources =
            dataSources.Where(dataSource => dataSource.Name != _dataSource.Name).ToImmutableList();
        var usedDataSources = EnumerableExtensions.GetFilteredConfigurationObjectList(
                otherDataSources, DataSourcePatterns,
                (dataSource, pattern) => Regex.IsMatch(dataSource.Name!, pattern),
                "dataSources")
            .Union(EnumerableExtensions.GetFilteredConfigurationObjectList(
                otherDataSources, DataSourceNames,
                (dataSource, name) => dataSource.Name == name,
                "dataSources")).ToImmutableList();

        _dataSource.Generator = generator;
        _dataSource.DataSourceList = usedDataSources;
        return _dataSource;
    }
}
