using ChannelsService.Application.DTOs;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Events;

namespace ChannelsService.Application.Interfaces;

public interface IChannelDeliveryService
{
    Task<DeliveryResultDTO> DeliverAsync(NotificationEvent notification, CancellationToken cancellationToken);
}