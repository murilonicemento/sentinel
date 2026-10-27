using AlertOrchestrator.Application.Interfaces.Messaging;
using StackExchange.Redis;

namespace AlertOrchestrator.Infrastructure.Idempotency;

/// <summary>
/// Claims use <c>SET NX</c> with a short lease, so the check and the claim are one atomic Redis operation across
/// instances. Keys are <c>alert-orchestrator:idempotency:v2:{eventId}</c>; the legacy <c>idempotency:{eventId}</c>
/// key written by the previous version is still honoured until it expires (24 h).
/// </summary>
public sealed class RedisIdempotencyService : IIdempotencyService
{
    private const string Processing = "processing";
    private const string Done = "done";

    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _retention;
    private readonly TimeSpan _lease;

    public RedisIdempotencyService(IConnectionMultiplexer redis, TimeSpan? retention = null, TimeSpan? lease = null)
    {
        _redis = redis;
        _retention = retention ?? TimeSpan.FromHours(24);
        _lease = lease ?? TimeSpan.FromMinutes(5);
    }

    public async Task<IdempotencyClaimResult> TryBeginAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var database = _redis.GetDatabase();

        if (await database.KeyExistsAsync(LegacyKey(eventId)))
            return IdempotencyClaimResult.AlreadyProcessed;

        if (await database.StringSetAsync(Key(eventId), Processing, _lease, When.NotExists))
            return IdempotencyClaimResult.Acquired;

        return await database.StringGetAsync(Key(eventId)) == Done
            ? IdempotencyClaimResult.AlreadyProcessed
            : IdempotencyClaimResult.InProgress;
    }

    public Task CompleteAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        _redis.GetDatabase().StringSetAsync(Key(eventId), Done, _retention);

    public async Task AbandonAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        // Releases only an unfinished claim, never a completed one.
        var transaction = _redis.GetDatabase().CreateTransaction();
        transaction.AddCondition(Condition.StringEqual(Key(eventId), Processing));
        _ = transaction.KeyDeleteAsync(Key(eventId));
        await transaction.ExecuteAsync();
    }

    private static string Key(Guid eventId) => $"alert-orchestrator:idempotency:v2:{eventId}";

    private static string LegacyKey(Guid eventId) => $"idempotency:{eventId}";
}
