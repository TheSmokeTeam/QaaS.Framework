using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
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
    private DataSource _dataSource;

    [Required, Description("Name of data source to reference it by (must be unique)")]
    public string? Name { get; internal set; }

    [Required, Description("The name of the generator to use")]
    internal string? Generator { get; set; }

    [Description("True to iterate over data lazily"), DefaultValue(false)]
    internal bool Lazy { get; set; } = false;

    [EnumerablePropertyDoesNotContainAnotherPropertyValue(nameof(Name)),
     Description("Names of data sources to pass to this data source for usage, those data sources dont have to be" +
                 " defined before this data source.")]
    internal string[] DataSourceNames { get; set; } = [];

    [EnumerablePropertyDoesNotContainAnotherPropertyValue(nameof(Name)),
     Description(
         "Regex patterns of data sources to pass to this data source for usage, those data sources dont have to be" +
         " defined before this data source.")]
    internal string[] DataSourcePatterns { get; set; } = [];

    [Description("Implementation configuration for the generator, " +
                 "the configuration given here is loaded into the provided generator dynamically.")]
    internal IConfiguration GeneratorConfiguration { get; set; } = new ConfigurationBuilder().Build();

    [Description("Serialize to use on the generated data"), DefaultValue(null)]
    [NullUnlessAll(new[] { nameof(Deserialize) }, [null])]
    internal SerializeConfig? Serialize { get; set; } = null;

    [Description("Deserialize to use on the generated data"), DefaultValue(null)]
    [NullUnlessAll(new[] { nameof(Serialize) }, [null])]
    internal DeserializeConfig? Deserialize { get; set; } = null;

    public DataSourceBuilder Named(string name)
    {
        Name = name;
        return this;
    }

    public DataSourceBuilder HookNamed(string hookName)
    {
        Generator = hookName;
        return this;
    }

    public DataSourceBuilder AddDataSourceName(string dataSourceName)
    {
        DataSourceNames = DataSourceNames.Append(dataSourceName).ToArray();
        return this;
    }

    public DataSourceBuilder AddDataSourcePattern(string dataSourcePattern)
    {
        DataSourcePatterns = DataSourcePatterns.Append(dataSourcePattern).ToArray();
        return this;
    }

    public DataSourceBuilder WithSerializer(SerializeConfig serializeConfig)
    {
        Serialize = serializeConfig;
        return this;
    }

    public DataSourceBuilder WithDeserializer(DeserializeConfig deserializeConfig)
    {
        Deserialize = deserializeConfig;
        return this;
    }

    public DataSourceBuilder IsLazy()
    {
        Lazy = true;
        return this;
    }

    public DataSourceBuilder Configure(object configuration)
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(configuration)));
        GeneratorConfiguration = new ConfigurationBuilder().AddJsonStream(stream).Build();
        return this;
    }

    public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        throw new NotSupportedException($"{nameof(Read)} doesn't support custom" +
                                        $" deserialization from Yaml for {nameof(DataSourceBuilder)}");
    }

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
            Serialize,
            Deserialize,
            GeneratorConfiguration = generatorConfiguration
        });
    }

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


    public DataSource Build(InternalContext context,IEnumerable<DataSource> dataSources,
        IEnumerable<KeyValuePair<string, IGenerator>> generators)
    {
        var generator = generators.FirstOrDefault(pair => pair.Key == Name!).Value ??
                        throw new ArgumentException($"Data source {Name}'s provided generator {Generator} was" +
                                                    $" not found in provided generators.");
        context.Logger.LogDebugWithMetaData("Started building Generator of type {type}", context.GetMetaDataFromContext(), Generator);
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