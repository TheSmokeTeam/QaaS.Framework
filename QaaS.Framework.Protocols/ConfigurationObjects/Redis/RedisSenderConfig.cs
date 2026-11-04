using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using StackExchange.Redis;

namespace QaaS.Framework.Protocols.ConfigurationObjects.Redis;

public record RedisSenderConfig : BaseRedisConfig, ISenderConfig
{
    [
        Description(
            "Send each chunk with a MULTI/EXEC transaction. False uses a pipelined nontransactional batch; partial writes are possible and QaaS does not automatically retry that batch. Every command result is observed in both modes."
        ),
        DefaultValue(true)
    ]
    public bool UseTransactions { get; set; } = true;

    [
        Required,
        Description(
            "Redis data type to use, define the function the qaas will use to send to the redis server"
        )
    ]
    public RedisDataType? RedisDataType { get; set; }

    [Description("Redis database to use"), DefaultValue(0)]
    public int RedisDataBase { get; set; } = 0;

    [
        Description("How many times to retry when failing to send an item, before crash"),
        DefaultValue(1)
    ]
    public int Retries { get; set; } = 1;

    [Description("Retries interval milliseconds"), DefaultValue(1000)]
    public int RetryIntervalMs { get; set; } = 1000;

    [
        Description(
            "This property does not partition sends. Configure Publisher.Chunk.ChunkSize (and optional MaxBytes) in Runner."
        ),
        DefaultValue(null)
    ]
    public int? BatchSize { get; set; } = null;

    [
        Description(
            "Indicates when this operation should be performed (only some variations are legal in a given context)"
        ),
        DefaultValue(When.Always)
    ]
    public When When { get; set; } = When.Always;

    [
        Description(
            "Specifies the command flags that should be performed, behaviour markers associated with a given command"
        ),
        DefaultValue(CommandFlags.None)
    ]
    public CommandFlags CommandFlags { get; set; } = CommandFlags.None;
}
