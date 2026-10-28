using System.Text.Json;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Domain.Interfaces;
using RiskEvaluation.Domain.ValueObjects;
using StackExchange.Redis;

namespace RiskEvaluation.Infrastructure.Caching;

public class RedisRecentScoresCache : IRecentScoresCache
{
    private const string KeyPrefix = "risk:score:";

    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _redis;
    private readonly ILogger<RedisRecentScoresCache> _logger;
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

    public RedisRecentScoresCache(IConnectionMultiplexer redis, ILogger<RedisRecentScoresCache> logger)
    {
        _connection = redis;
        _redis = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<RiskScoreCacheEntry?> GetAsync(int latitude, int longitude, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(latitude, longitude);
        var value = await _redis.StringGetAsync(key);

        if (value.IsNullOrEmpty)
        {
            return null;
        }

        try
        {
            var entry = JsonSerializer.Deserialize<RiskScoreCacheEntry>(value!);
            return entry;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize cache entry for {Latitude},{Longitude}", latitude, longitude);
            return null;
        }
    }

    public async Task SetAsync(int latitude, int longitude, RiskScoreCacheEntry entry, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(latitude, longitude);
        var value = JsonSerializer.Serialize(entry);
        var expiry = expiration ?? DefaultExpiration;

        await _redis.StringSetAsync(key, value, expiry);
    }

    public async Task<long> InvalidateAllAsync(CancellationToken cancellationToken = default)
    {
        long removed = 0;
        // SCAN (not KEYS) walks the keyspace incrementally without blocking Redis.
        foreach (var server in _connection.GetServers().Where(s => s.IsConnected && !s.IsReplica))
            await foreach (var key in server.KeysAsync(_redis.Database, $"{KeyPrefix}*", pageSize: 500)
                               .WithCancellation(cancellationToken))
                if (await _redis.KeyDeleteAsync(key))
                    removed++;

        return removed;
    }

    private static string BuildKey(int latitude, int longitude) => $"{KeyPrefix}{latitude}:{longitude}";
}
