using ChannelsService.Domain.Entities;

namespace ChannelsService.Application.Interfaces;

public interface IDeliveryRepository
{
    Task AddAsync(DeliveryAttempt attempt);
    Task<IReadOnlyList<DeliveryAttempt>> GetByEventAsync(string eventId);
}