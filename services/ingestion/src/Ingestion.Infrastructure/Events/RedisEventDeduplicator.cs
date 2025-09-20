using Ingestion.Application.Events;
using StackExchange.Redis;

namespace Ingestion.Infrastructure.Events;

public class RedisEventDeduplicator : IEventDeduplicator
{
    private readonly IDatabase _redis;

    public RedisEventDeduplicator(IConnectionMultiplexer connectionMultiplexer)
    {
        _redis = connectionMultiplexer.GetDatabase();
    }

    public async Task<bool> IsDuplicateAsync(string key) =>
        await _redis.KeyExistsAsync(key);

    public async Task MarkAsProcessedAsync(string key, TimeSpan timeToLive) =>
        await _redis.StringSetAsync(key, "1", timeToLive);
}