using StackExchange.Redis;

namespace RiskEvaluation.Infrastructure.Messaging;

public enum ConsumedMessageClaim
{
    Acquired,
    AlreadyProcessed,
    InProgress
}

/// <summary>
/// Atomic claim on a consumed message, so a redelivered or concurrently consumed message is evaluated once. Keys are
/// <c>risk-evaluation:consumed:v1:{messageKey}</c>: <c>SET NX</c> with a short lease while processing, then a retention
/// marker. A claim left by a crashed process expires with its lease.
/// </summary>
public interface IConsumedMessageGuard
{
    Task<ConsumedMessageClaim> TryBeginAsync(string messageKey);
    Task CompleteAsync(string messageKey);
    Task AbandonAsync(string messageKey);
}

public sealed class RedisConsumedMessageGuard : IConsumedMessageGuard
{
    private const string Processing = "processing";
    private const string Done = "done";

    private readonly TimeSpan _lease;
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _retention;

    public RedisConsumedMessageGuard(IConnectionMultiplexer redis, TimeSpan? retention = null, TimeSpan? lease = null)
    {
        _redis = redis;
        _retention = retention ?? TimeSpan.FromHours(24);
        _lease = lease ?? TimeSpan.FromMinutes(5);
    }

    public async Task<ConsumedMessageClaim> TryBeginAsync(string messageKey)
    {
        var database = _redis.GetDatabase();
        if (await database.StringSetAsync(Key(messageKey), Processing, _lease, When.NotExists))
            return ConsumedMessageClaim.Acquired;

        return await database.StringGetAsync(Key(messageKey)) == Done
            ? ConsumedMessageClaim.AlreadyProcessed
            : ConsumedMessageClaim.InProgress;
    }

    public Task CompleteAsync(string messageKey) =>
        _redis.GetDatabase().StringSetAsync(Key(messageKey), Done, _retention);

    public async Task AbandonAsync(string messageKey)
    {
        var transaction = _redis.GetDatabase().CreateTransaction();
        transaction.AddCondition(Condition.StringEqual(Key(messageKey), Processing));
        _ = transaction.KeyDeleteAsync(Key(messageKey));
        await transaction.ExecuteAsync();
    }

    private static string Key(string messageKey) => $"risk-evaluation:consumed:v1:{messageKey}";
}

/// <summary>Raised when another consumer is processing the same message; consumers treat it as transient.</summary>
public sealed class MessageInProgressException(string messageKey)
    : Exception($"Message {messageKey} is being processed by another consumer.");
