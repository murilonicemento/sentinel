using ChannelsService.Application.DTOs;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Events;

namespace ChannelsService.Application.Interfaces;

public interface IFallbackExecutor
{
    IReadOnlyList<ChannelTypeEnum> GetFallbackOrder(NotificationEvent notification, TenantChannelDTO dto);
}