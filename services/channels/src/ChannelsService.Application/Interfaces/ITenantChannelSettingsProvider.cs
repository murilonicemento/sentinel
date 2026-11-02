using ChannelsService.Application.DTOs;
using ChannelsService.Domain.Entities;

namespace ChannelsService.Application.Interfaces;

public interface ITenantChannelSettingsProvider
{
    TenantChannelDTO GetSettings(string tenantId);
}