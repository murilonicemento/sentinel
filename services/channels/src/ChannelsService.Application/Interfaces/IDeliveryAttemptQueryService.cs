using ChannelsService.Application.DTOs;
using ChannelsService.Domain.Entities;

namespace ChannelsService.Application.Interfaces;

public interface IDeliveryAttemptQueryService
{
    Task<IReadOnlyList<DeliveryAttempt>> GetByEventAsync(string eventId, TenantAccessScope scope,
        CancellationToken cancellationToken);
}
