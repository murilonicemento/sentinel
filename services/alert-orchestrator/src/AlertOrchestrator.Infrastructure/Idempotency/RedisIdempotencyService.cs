using AlertOrchestrator.Application.Interfaces.Messaging;
using Microsoft.Extensions.Caching.Distributed;

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

    private static string GetKey(Guid eventId)
    {
        return $"idempotency:{eventId}";
    }
}