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

    internal RedisProtocol(RedisSenderConfig configuration, ILogger logger, IDatabase database)
        : this(configuration, logger) => _redisDb = database;

    public IEnumerable<DetailedData<object>> SendChunk(IEnumerable<Data<object>> chunkDataToSend)
    {
        if (_redisDb == null)
            throw new InvalidOperationException(
                "Redis is not connected. Call Connect() before sending data."
            );

        var toSend = chunkDataToSend.ToArray();
        if (!configuration.UseTransactions)
        {
            var batch = _redisDb.CreateBatch();
            var commands = toSend
                .Select(message => QueueCommand(batch, message.CastObjectData<byte[]>()))
                .ToArray();
            batch.Execute();
            // A pipeline has no rollback. Never replay successful commands after a partial failure.
            Task.WhenAll(commands).GetAwaiter().GetResult();
            return toSend.Select(message => message.CloneDetailed()).ToImmutableList();
        }
        var retryCount = 0;
        for (; retryCount < configuration.Retries; retryCount++)
        {
            // Build a fresh transaction for every attempt: a StackExchange.Redis ITransaction cannot be
            // re-executed once it has run, so reusing one across retries would send an empty/invalid batch.
            var redisTransaction = _redisDb.CreateTransaction();
            var commands = toSend
                .Select(message =>
                    AddToRedisTransactionByRedisType(
                        ref redisTransaction,
                        message.CastObjectData<byte[]>()
                    )
                )
                .ToArray();
            try
            {
                var result = redisTransaction.ExecuteAsync();
                var committed = result.ConfigureAwait(false).GetAwaiter().GetResult();
                if (!committed || result.Exception?.InnerExceptions.Count > 0)
                    throw new RedisException(
                        result.Exception?.ToString()
                            ?? "Redis transaction did not commit (Execute returned false with no exception)."
                    );
            }
            catch (Exception redisException)
            {
                logger.LogError("Failed to send to redis \n {RedisException}", redisException);
                // The for-loop header increments the retry counter after this delay.
                Thread.Sleep(configuration.RetryIntervalMs);
                continue;
            }
            // EXEC success alone does not mean every command succeeded. Observe command errors
            // outside the retry block: MULTI/EXEC does not roll back successful sibling commands.
            Task.WhenAll(commands).GetAwaiter().GetResult();
            logger.LogDebug("Sent batch of size {BatchSize} to Redis successfully", toSend.Length);
            return toSend.Select(message => message.CloneDetailed()).ToImmutableList();
        }

        throw new RedisException($"Failed to send to redis after {retryCount} retries");
    }

    public SerializationType? GetSerializationType() => null;

    private Task AddToRedisTransactionByRedisType(
        ref ITransaction redisTransaction,
        Data<byte[]> dataToSend
    ) => QueueCommand(redisTransaction, dataToSend);

    private Task QueueCommand(IDatabaseAsync redisTransaction, Data<byte[]> dataToSend)
    {
        var redisKey = dataToSend.MetaData?.Redis?.Key;
        switch (configuration.RedisDataType)
        {
            case RedisDataType.SetString:
                return redisTransaction.StringSetAsync(
                    redisKey,
                    dataToSend.Body,
                    when: configuration.When,
                    flags: configuration.CommandFlags
                );

            case RedisDataType.ListLeftPush:
                return redisTransaction.ListLeftPushAsync(
                    redisKey,
                    dataToSend.Body,
                    when: configuration.When,
                    flags: configuration.CommandFlags
                );

            case RedisDataType.ListRightPush:
                return redisTransaction.ListRightPushAsync(
                    redisKey,
                    dataToSend.Body,
                    configuration.When,
                    flags: configuration.CommandFlags
                );

            case RedisDataType.SetAdd:
                return redisTransaction.SetAddAsync(redisKey, dataToSend.Body);

            case RedisDataType.HashSet:
                var hashEntries = new HashEntry[]
                {
                    new(
                        dataToSend.MetaData?.Redis?.HashField
                            ?? throw new ArgumentException(
                                $"Hash field missing for {RedisDataType.HashSet} action"
                            ),
                        dataToSend.Body
                    ),
                };
                return redisTransaction.HashSetAsync(
                    redisKey,
                    hashEntries,
                    flags: configuration.CommandFlags
                );

            case RedisDataType.SortedSetAdd:
                return redisTransaction.SortedSetAddAsync(
                    redisKey,
                    dataToSend.Body,
                    dataToSend.MetaData?.Redis?.SetScore
                        ?? throw new ArgumentException(
                            $"Set score missing for {RedisDataType.SortedSetAdd} action"
                        ),
                    flags: configuration.CommandFlags
                );

            case RedisDataType.GeoAdd:
                return redisTransaction.GeoAddAsync(
                    redisKey,
                    new GeoEntry(
                        dataToSend.MetaData?.Redis?.GeoLongitude
                            ?? throw new ArgumentException(
                                $"Geo longitude missing for {RedisDataType.GeoAdd} action"
                            ),
                        dataToSend.MetaData?.Redis?.GeoLatitude
                            ?? throw new ArgumentException(
                                $"Geo latitude missing for {RedisDataType.GeoAdd} action"
                            ),
                        dataToSend.Body
                    ),
                    flags: configuration.CommandFlags
                );
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(configuration.RedisDataType),
                    configuration.RedisDataType,
                    "RedisDataType given is not supported"
                );
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
