using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Events;

namespace ChannelsService.Application.Services;

public sealed class FallbackExecutor : IFallbackExecutor
{
    public IReadOnlyList<ChannelTypeEnum> GetFallbackOrder(NotificationEvent notification, TenantChannelDTO dto)
    {
        var requestedChannels = notification.Channels?.Distinct().ToList() ?? new List<ChannelTypeEnum>();
        var enabledChannels = dto.EnabledChannels.Any() ? dto.EnabledChannels : Enum.GetValues<ChannelTypeEnum>().Cast<ChannelTypeEnum>().ToList();

        var candidateChannels = requestedChannels.Any()
            ? requestedChannels.Where(dto.EnabledChannels.Contains).ToList()
            : enabledChannels;

        if (dto.FallbackOrder.Any() && notification.FallbackEnabled)
        {
            return dto.FallbackOrder
                .Where(candidateChannels.Contains)
                .Distinct()
                .ToList();
        }

        return candidateChannels
            .Distinct()
            .OrderBy(channel => dto.PriorityOrder.GetValueOrDefault(channel, int.MaxValue))
            .ToList();
    }
}