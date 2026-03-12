using System.CodeDom.Compiler;
using System.Linq;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Protocols.ConfigurationObjects.Redis;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.MetaDataObjects;
using QaaS.Framework.Serialization;
using StackExchange.Redis;

namespace QaaS.Framework.Protocols.Protocols;

public class RedisReaderProtocol(RedisReaderConfig configuration, ILogger logger) : IReader
{
    private IDatabase? _redisDb;
    private ConnectionMultiplexer? _redisConnection;

    public DetailedData<object>? Read(TimeSpan timeout)
    {
        if (_redisDb == null)
            throw new InvalidOperationException("Redis is not connected. Call Connect() before reading data.");

        var startedAt = DateTime.UtcNow;
        while (DateTime.UtcNow - startedAt < timeout)
        {
            var data = TryRead();
            if (data != null)
            {
                logger.LogDebug("Consumed redis item from key {RedisKey} using {RedisDataType}",
                    configuration.Key, configuration.RedisDataType);
                return data;
            }

            Thread.Sleep(configuration.PollIntervalMs);
        }

        return null;
    }

    public SerializationType? GetSerializationType() => null;

    public void Connect()
    {
        var configurationOptions = configuration.CreateRedisConfigurationOptions();
        TextWriter consoleWriter = new IndentedTextWriter(Console.Out);
        _redisConnection = ConnectionMultiplexer.Connect(configurationOptions, consoleWriter);
        _redisDb = _redisConnection.GetDatabase(configuration.RedisDataBase);
    }

    public void Disconnect()
    {
        _redisConnection?.Close();
        _redisConnection?.Dispose();
    }

    private DetailedData<object>? TryRead()
    {
        return configuration.RedisDataType switch
        {
            RedisDataType.SetString => ReadString(),
            RedisDataType.ListLeftPush => ReadListLeft(),
            RedisDataType.ListRightPush => ReadListRight(),
            RedisDataType.SetAdd => ReadSet(),
            RedisDataType.HashSet => ReadHash(),
            RedisDataType.SortedSetAdd => ReadSortedSet(),
            RedisDataType.GeoAdd => throw new NotSupportedException(
                "Redis GeoAdd consumption is not supported. Use a list, string, set, hash, or sorted set reader instead."),
            null => throw new InvalidOperationException("Redis data type is required for redis consumption."),
            _ => throw new ArgumentOutOfRangeException(nameof(configuration.RedisDataType), configuration.RedisDataType,
                "Redis data type is not supported for consumption")
        };
    }

    private DetailedData<object>? ReadString()
    {
        var value = _redisDb!.StringGetDelete(configuration.Key, configuration.CommandFlags);
        if (value.IsNull)
        {
            return null;
        }

        return CreateDetailedData(value, new Redis { Key = configuration.Key });
    }

    private DetailedData<object>? ReadListLeft()
    {
        var value = _redisDb!.ListLeftPop(configuration.Key, configuration.CommandFlags);
        return value.IsNull ? null : CreateDetailedData(value, new Redis { Key = configuration.Key });
    }

    private DetailedData<object>? ReadListRight()
    {
        var value = _redisDb!.ListRightPop(configuration.Key, configuration.CommandFlags);
        return value.IsNull ? null : CreateDetailedData(value, new Redis { Key = configuration.Key });
    }

    private DetailedData<object>? ReadSet()
    {
        var value = _redisDb!.SetPop(configuration.Key, configuration.CommandFlags);
        return value.IsNull ? null : CreateDetailedData(value, new Redis { Key = configuration.Key });
    }

    private DetailedData<object>? ReadHash()
    {
        if (string.IsNullOrWhiteSpace(configuration.HashField))
            throw new ArgumentException($"Hash field missing for {RedisDataType.HashSet} action");

        var value = _redisDb!.HashGet(configuration.Key, configuration.HashField, configuration.CommandFlags);
        if (value.IsNull)
        {
            return null;
        }

        _redisDb.HashDelete(configuration.Key, configuration.HashField, configuration.CommandFlags);
        return CreateDetailedData(value, new Redis
        {
            Key = configuration.Key,
            HashField = configuration.HashField
        });
    }

    private DetailedData<object>? ReadSortedSet()
    {
        var entry = _redisDb!.SortedSetRangeByRankWithScores(configuration.Key, 0, 0, configuration.SortedSetOrder,
            configuration.CommandFlags).FirstOrDefault();
        if (entry.Element.IsNull)
        {
            return null;
        }

        _redisDb.SortedSetRemove(configuration.Key, entry.Element, configuration.CommandFlags);
        return CreateDetailedData(entry.Element, new Redis
        {
            Key = configuration.Key,
            SetScore = entry.Score
        });
    }

    private static DetailedData<object>? CreateDetailedData(RedisValue value, Redis redisMetaData)
    {
        var bytes = (byte[]?)value;
        if (bytes == null)
        {
            return null;
        }

        return new DetailedData<object>
        {
            Body = bytes,
            Timestamp = DateTime.UtcNow,
            MetaData = new MetaData
            {
                Redis = redisMetaData
            }
        };
    }
}
