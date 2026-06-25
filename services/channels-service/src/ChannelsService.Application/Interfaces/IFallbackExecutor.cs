using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;

namespace ChannelsService.Application.Interfaces;

public interface IFallbackExecutor
{
    IReadOnlyList<ChannelTypeEnum> GetFallbackOrder(NotificationEvent notification, TenantChannelSettings settings);
}