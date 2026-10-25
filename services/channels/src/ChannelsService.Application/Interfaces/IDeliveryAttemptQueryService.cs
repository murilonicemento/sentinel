using ChannelsService.Domain.Entities;

namespace ChannelsService.Application.Interfaces;

public interface IDeliveryAttemptQueryService
{
    Task<IReadOnlyList<DeliveryAttempt>> GetByEventAsync(string eventId, CancellationToken cancellationToken);
}
