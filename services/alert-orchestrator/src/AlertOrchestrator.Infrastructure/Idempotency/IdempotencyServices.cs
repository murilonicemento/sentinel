using AlertOrchestrator.Application.Interfaces.Messaging;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace AlertOrchestrator.Infrastructure.Idempotency;

public sealed class RedisIdempotencyService : IIdempotencyService
{
    private readonly IDistributedCache _cache;
    private readonly TimeSpan _expirationTime;

    public RedisIdempotencyService(IDistributedCache cache, TimeSpan? expirationTime = null)
    {
        _cache = cache;
        _expirationTime = expirationTime ?? TimeSpan.FromHours(24);
    }

    public async Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var key = GetKey(eventId);
        var value = await _cache.GetStringAsync(key, cancellationToken);
        return !string.IsNullOrEmpty(value);
    }

    public async Task MarkAsProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var key = GetKey(eventId);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _expirationTime
        };
        await _cache.SetStringAsync(key, "processed", options, cancellationToken);
    }

    private static string GetKey(Guid eventId) => $"idempotency:{eventId}";
}

public sealed class InMemoryIdempotencyService : IIdempotencyService
{
    private readonly HashSet<Guid> _processedEvents = new();
    private readonly TimeSpan _expirationTime;
    private readonly Dictionary<Guid, DateTime> _eventTimestamps = new();
    private readonly Lock _lock = new();

    public InMemoryIdempotencyService(TimeSpan? expirationTime = null)
    {
        _expirationTime = expirationTime ?? TimeSpan.FromHours(24);
    }

    public Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            CleanupExpired();
            return Task.FromResult(_processedEvents.Contains(eventId));
        }
    }

    public Task MarkAsProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            CleanupExpired();
            _processedEvents.Add(eventId);
            _eventTimestamps[eventId] = DateTime.UtcNow;
        }
        return Task.CompletedTask;
    }

    private void CleanupExpired()
    {
        var cutoff = DateTime.UtcNow - _expirationTime;
        var expired = _eventTimestamps
            .Where(x => x.Value < cutoff)
            .Select(x => x.Key)
            .ToList();

        foreach (var eventId in expired)
        {
            _processedEvents.Remove(eventId);
            _eventTimestamps.Remove(eventId);
        }
    }
}
