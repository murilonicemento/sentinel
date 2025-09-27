using Ingestion.Application.Interfaces.Events;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Ingestion.Infrastructure.Events;

public class RedisEventDeduplicator : IEventDeduplicator
{
    private readonly IDatabase _redis;
    private readonly ILogger<RedisEventDeduplicator> _logger;

    public RedisEventDeduplicator(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisEventDeduplicator> logger)
    {
        _redis = connectionMultiplexer.GetDatabase();
        _logger = logger;
    }

    public async Task<bool> IsDuplicateAsync(string key)
    {
        try
        {
            return await _redis.KeyExistsAsync(key);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to get data in cache with key {key}", key);
            throw;
        }
    }


    public async Task MarkAsProcessedAsync(string key, TimeSpan timeToLive) =>
        await _redis.StringSetAsync(key, "1", timeToLive);
}