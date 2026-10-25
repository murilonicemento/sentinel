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

    public Task<IReadOnlyList<DeliveryAttempt>> GetByEventAsync(string eventId, CancellationToken cancellationToken)
        => _deliveryRepository.GetByEventAsync(eventId);
}
