using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using StackExchange.Redis;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Redis;

public record RedisReaderConfig : BaseRedisConfig, IReaderConfig
{
    [Required, Description("Redis data type to use, define the function the qaas will use to consume from the redis server")]
    public RedisDataType? RedisDataType { get; set; }

    [Required, Description("Redis key to consume from")]
    public string Key { get; set; } = string.Empty;

    [Description("Redis database to use"), DefaultValue(0)]
    public int RedisDataBase { get; set; } = 0;

    [Description("Hash field to consume when using HashSet"), DefaultValue(null)]
    public string? HashField { get; set; }

    [Description("Order in which to consume sorted set items when using SortedSetAdd"), DefaultValue(Order.Ascending)]
    public Order SortedSetOrder { get; set; } = Order.Ascending;

    [Description("Time in milliseconds between polling attempts while waiting for the next redis item"), DefaultValue(100)]
    [Range(1, int.MaxValue)]
    public int PollIntervalMs { get; set; } = 100;

    [Description("Specifies the command flags that should be performed, behaviour markers associated with a given command"), DefaultValue(CommandFlags.None)]
    public CommandFlags CommandFlags { get; set; } = CommandFlags.None;
}
