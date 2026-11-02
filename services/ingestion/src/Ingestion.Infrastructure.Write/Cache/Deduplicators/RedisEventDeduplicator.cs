using Ingestion.Application.Interfaces.Deduplicators;
using StackExchange.Redis;

namespace Ingestion.Infrastructure.Write.Cache.Deduplicators;

/// <summary>
/// Claims with <c>SET NX</c>, so the duplicate check and the claim are a single atomic Redis operation across
/// instances. The processed marker keeps the previous value ("1"), which older versions read with EXISTS.
/// </summary>
public class RedisEventDeduplicator : IEventDeduplicator
{
    private const string Processing = "processing";

    private readonly IDatabase _redis;

    public RedisEventDeduplicator(IConnectionMultiplexer connectionMultiplexer)
    {
        _redis = connectionMultiplexer.GetDatabase();
    }

    public Task<bool> TryBeginAsync(string key, TimeSpan lease) =>
        _redis.StringSetAsync(key, Processing, lease, When.NotExists);

    public async Task MarkAsProcessedAsync(string key, TimeSpan timeToLive) =>
        await _redis.StringSetAsync(key, "1", timeToLive);

    public async Task AbandonAsync(string key)
    {
        var transaction = _redis.CreateTransaction();
        transaction.AddCondition(Condition.StringEqual(key, Processing));
        _ = transaction.KeyDeleteAsync(key);
        await transaction.ExecuteAsync();
    }
}
