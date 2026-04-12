using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Models;

namespace ChannelsService.Application.Services;

public sealed class FallbackExecutor : IFallbackExecutor
{
    public IReadOnlyList<ChannelType> GetFallbackOrder(NotificationEvent notification, TenantChannelSettings settings)
    {
        var requestedChannels = notification.Channels?.Distinct().ToList() ?? new List<ChannelType>();
        var enabledChannels = settings.EnabledChannels.Any() ? settings.EnabledChannels : Enum.GetValues<ChannelType>().Cast<ChannelType>().ToList();

        var candidateChannels = requestedChannels.Any()
            ? requestedChannels.Where(settings.EnabledChannels.Contains).ToList()
            : enabledChannels;

        if (settings.FallbackOrder.Any())
        {
            return settings.FallbackOrder
                .Where(candidateChannels.Contains)
                .Distinct()
                .ToList();
        }

        return candidateChannels.Distinct().ToList();
    }
}