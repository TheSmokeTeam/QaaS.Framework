using System.CodeDom.Compiler;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Protocols.ConfigurationObjects.Redis;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.Serialization;
using StackExchange.Redis;

namespace QaaS.Framework.Protocols.Protocols;

public class RedisProtocol(RedisSenderConfig configuration, ILogger logger) : IChunkSender
{
    private IDatabase? _redisDb;
    private ConnectionMultiplexer? _redisConnection;

    public IEnumerable<DetailedData<object>> SendChunk(IEnumerable<Data<object>> chunkDataToSend)
    {
        var retryCount = 0;
        var redisTransaction = _redisDb.CreateTransaction();
        var toSend = chunkDataToSend.ToArray();
        foreach (var message in toSend)
            AddToRedisTransactionByRedisType(ref redisTransaction,
                message.CastObjectData<byte[]>()); // Assumes data is byte[]
        for (; retryCount < configuration.Retries; retryCount++)
        {
            try
            {
                var result = redisTransaction.ExecuteAsync();
                if (!result.Result || result.Exception?.InnerExceptions.Count > 0)
                    throw new RedisException(result.Exception!.ToString());
                logger.LogDebug("Sended batch of size {BatchSize} to redis successfully", toSend.Length);
                return toSend.Select(message => message.CloneDetailed()).ToImmutableList();
            }
            catch (Exception redisException)
            {
                logger.LogError("Failed to send to redis \n {RedisException}", redisException);
                retryCount++;
                Thread.Sleep(configuration.RetryIntervalMs);
            }
        }

        throw new RedisException($"Failed to send to redis after {retryCount} retries");
    }

    public SerializationType? GetSerializationType() => null;

    private void AddToRedisTransactionByRedisType(ref ITransaction redisTransaction, Data<byte[]> dataToSend)
    {
        var redisKey = dataToSend.MetaData?.Redis?.Key;
        switch (configuration.RedisDataType)
        {
            case RedisDataType.SetString:
                redisTransaction.StringSetAsync(redisKey,
                    dataToSend.Body, when: configuration.When, flags: configuration.CommandFlags);
                break;

            case RedisDataType.ListLeftPush:
                redisTransaction.ListLeftPushAsync(redisKey,
                    dataToSend.Body, when: configuration.When, flags: configuration.CommandFlags);
                break;

            case RedisDataType.ListRightPush:
                redisTransaction.ListRightPushAsync(redisKey,
                    dataToSend.Body, configuration.When, flags: configuration.CommandFlags);
                break;

            case RedisDataType.SetAdd:
                redisTransaction.SetAddAsync(redisKey,
                    dataToSend.Body);
                break;

            case RedisDataType.HashSet:
                var hashEntries = new HashEntry[]
                {
                    new(dataToSend.MetaData?.Redis?.HashField ??
                        throw new ArgumentException($"Hash field missing for {RedisDataType.HashSet} action"),
                        dataToSend.Body)
                };
                redisTransaction.HashSetAsync(redisKey, hashEntries, flags: configuration.CommandFlags);
                break;

            case RedisDataType.SortedSetAdd:
                redisTransaction.SortedSetAddAsync(redisKey, dataToSend.Body,
                    dataToSend.MetaData?.Redis?.SetScore ??
                    throw new ArgumentException($"Set score missing for {RedisDataType.SortedSetAdd} action"),
                    flags: configuration.CommandFlags);
                break;

            case RedisDataType.GeoAdd:
                redisTransaction.GeoAddAsync(redisKey,
                    new GeoEntry(
                        dataToSend.MetaData?.Redis?.GeoLongitude ??
                        throw new ArgumentException($"Geo longitude missing for {RedisDataType.GeoAdd} action"),
                        dataToSend.MetaData?.Redis?.GeoLatitude ??
                        throw new ArgumentException($"Geo latitude missing for {RedisDataType.GeoAdd} action"),
                        dataToSend.Body), flags: configuration.CommandFlags);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(configuration.RedisDataType), configuration.RedisDataType,
                    "RedisDataType given is not supported");
        }
    }

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
}