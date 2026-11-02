using AlertOrchestrator.Application.Interfaces.Messaging;

namespace AlertOrchestrator.Infrastructure.Idempotency;

public sealed class InMemoryIdempotencyService : IIdempotencyService
{
    private readonly Dictionary<Guid, DateTime> _eventTimestamps = new();
    private readonly TimeSpan _expirationTime;
    private readonly Lock _lock = new();
    private readonly HashSet<Guid> _processedEvents = new();

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