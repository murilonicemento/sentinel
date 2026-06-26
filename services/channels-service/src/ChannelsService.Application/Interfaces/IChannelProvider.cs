using ChannelsService.Application.DTOs;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Events;

namespace ChannelsService.Application.Interfaces;

public interface IChannelProvider
{
    ChannelTypeEnum ChannelTypeEnum { get; }
    string ProviderName { get; }
    Task<DeliveryResultDTO> SendAsync(NotificationEvent notification, CancellationToken cancellationToken);
}
