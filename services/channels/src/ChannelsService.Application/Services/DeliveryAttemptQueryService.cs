using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Entities;

namespace ChannelsService.Application.Services;

public sealed class DeliveryAttemptQueryService : IDeliveryAttemptQueryService
{
    private readonly IDeliveryRepository _deliveryRepository;

    public DeliveryAttemptQueryService(IDeliveryRepository deliveryRepository)
    {
        _deliveryRepository = deliveryRepository;
    }

    public async Task<IReadOnlyList<DeliveryAttempt>> GetByEventAsync(string eventId, TenantAccessScope scope,
        CancellationToken cancellationToken)
    {
        var attempts = await _deliveryRepository.GetByEventAsync(eventId);
        return attempts.Where(attempt => scope.Allows(attempt.TenantId)).ToList();
    }
}
