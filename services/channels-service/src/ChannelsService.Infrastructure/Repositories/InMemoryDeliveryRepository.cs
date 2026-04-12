using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Models;

namespace ChannelsService.Infrastructure.Repositories;

public sealed class InMemoryDeliveryRepository : IDeliveryRepository
{
    private readonly List<DeliveryAttempt> _attempts = new();
    private readonly object _lock = new();

    public Task AddAsync(DeliveryAttempt attempt)
    {
        lock (_lock)
        {
            _attempts.Add(attempt);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeliveryAttempt>> GetByEventAsync(string eventId)
    {
        IReadOnlyList<DeliveryAttempt> result;

        lock (_lock)
        {
            result = _attempts
                .Where(attempt => attempt.EventId == eventId)
                .ToList();
        }

        return Task.FromResult(result);
    }
}
