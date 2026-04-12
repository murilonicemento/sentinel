using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Models;

namespace ChannelsService.Application.Interfaces;

public interface IFallbackExecutor
{
    IReadOnlyList<ChannelType> GetFallbackOrder(NotificationEvent notification, TenantChannelSettings settings);
}