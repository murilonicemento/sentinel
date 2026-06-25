using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;

namespace ChannelsService.Application.Services;

public sealed class FallbackExecutor : IFallbackExecutor
{
    public IReadOnlyList<ChannelTypeEnum> GetFallbackOrder(NotificationEvent notification, TenantChannelSettings settings)
    {
        var requestedChannels = notification.Channels?.Distinct().ToList() ?? new List<ChannelTypeEnum>();
        var enabledChannels = settings.EnabledChannels.Any() ? settings.EnabledChannels : Enum.GetValues<ChannelTypeEnum>().Cast<ChannelTypeEnum>().ToList();

        var candidateChannels = requestedChannels.Any()
            ? requestedChannels.Where(settings.EnabledChannels.Contains).ToList()
            : enabledChannels;

        if (settings.FallbackOrder.Any() && notification.FallbackEnabled)
        {
            return settings.FallbackOrder
                .Where(candidateChannels.Contains)
                .Distinct()
                .ToList();
        }

        return candidateChannels
            .Distinct()
            .OrderBy(channel => settings.PriorityOrder.GetValueOrDefault(channel, int.MaxValue))
            .ToList();
    }
}