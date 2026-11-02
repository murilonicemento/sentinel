using AlertOrchestrator.Application.Interfaces.Messaging;

namespace AlertOrchestrator.Infrastructure.Idempotency;

/// <summary>Single-instance fallback used when Redis is disabled; claims do not survive a restart.</summary>
public sealed class InMemoryIdempotencyService : IIdempotencyService
{
    private readonly Dictionary<Guid, (bool Done, DateTime ExpiresAt)> _claims = new();
    private readonly TimeSpan _lease;
    private readonly Lock _lock = new();
    private readonly TimeSpan _retention;
    private readonly TimeProvider _timeProvider;

    public InMemoryIdempotencyService(TimeSpan? retention = null, TimeSpan? lease = null,
        TimeProvider? timeProvider = null)
    {
        _retention = retention ?? TimeSpan.FromHours(24);
        _lease = lease ?? TimeSpan.FromMinutes(5);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<IdempotencyClaimResult> TryBeginAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            if (_claims.TryGetValue(eventId, out var claim) && claim.ExpiresAt > now)
                return Task.FromResult(claim.Done
                    ? IdempotencyClaimResult.AlreadyProcessed
                    : IdempotencyClaimResult.InProgress);

            RemoveExpired(now);
            _claims[eventId] = (false, now.Add(_lease));
            return Task.FromResult(IdempotencyClaimResult.Acquired);
        }
    }

    public Task CompleteAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
            _claims[eventId] = (true, _timeProvider.GetUtcNow().UtcDateTime.Add(_retention));
        return Task.CompletedTask;
    }

    public Task AbandonAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
            if (_claims.TryGetValue(eventId, out var claim) && !claim.Done)
                _claims.Remove(eventId);
        return Task.CompletedTask;
    }

    private void RemoveExpired(DateTime now)
    {
        foreach (var expired in _claims.Where(c => c.Value.ExpiresAt <= now).Select(c => c.Key).ToList())
            _claims.Remove(expired);
    }
}
